using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Mapping;

/// <summary>
///     Handles autosaving maps.
/// </summary>
public sealed class MappingSystem : EntitySystem
{
    private static readonly UTF8Encoding AutosaveEncoding = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    [Dependency] private readonly IConsoleHost _conHost = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IResourceManager _resMan = default!;
    [Dependency] private readonly MapLoaderSystem _loader = default!;
    [Dependency] private readonly MappingSnapshotSystem _snapshots = default!;

    // Not a comp because I don't want to deal with this getting saved onto maps ever
    /// <summary>
    ///     map id -> next autosave timespan & original filename.
    /// </summary>
    /// <returns></returns>
    private readonly Dictionary<EntityUid, (TimeSpan next, string fileName)> _currentlyAutosaving = new();

    private bool _autosaveEnabled;

    public override void Initialize()
    {
        base.Initialize();

        _conHost.RegisterCommand("toggleautosave",
            "Toggles autosaving for a map.",
            "autosave <map> <path if enabling>",
            ToggleAutosaveCommand);

        Subs.CVar(_cfg, CCVars.AutosaveEnabled, SetAutosaveEnabled, true);
    }

    private void SetAutosaveEnabled(bool b)
    {
        if (!b)
            _currentlyAutosaving.Clear();
        _autosaveEnabled = b;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_autosaveEnabled)
            return;

        List<(EntityUid Uid, string Name)>? due = null;
        foreach (var (uid, (time, name)) in _currentlyAutosaving)
        {
            if (_timing.RealTime <= time)
                continue;

            due ??= [];
            due.Add((uid, name));
        }

        if (due == null)
            return;

        foreach (var (uid, name) in due)
        {
            if (Deleted(uid))
            {
                Log.Warning($"Can't autosave deleted entity {uid}. Removing it from autosave.");
                _currentlyAutosaving.Remove(uid);
                continue;
            }

            if (LifeStage(uid) >= EntityLifeStage.MapInitialized && !HasComp<MapComponent>(uid))
            {
                Log.Warning($"Can't autosave initialized grid {uid} without its map root. Removing it from autosave.");
                _currentlyAutosaving.Remove(uid);
                continue;
            }

            _currentlyAutosaving[uid] = (CalculateNextTime(), name);
            if (!TryAutosaveNow(uid, name, out var path, out var report, out var error))
            {
                Log.Error($"Failed to autosave {name} ({uid}): {error}. " +
                          $"Next attempt in {ReadableTimeLeft(uid)} seconds.");
                continue;
            }

            var snapshot = report.ValidatedEntities > 0
                ? $" Validated {report.ValidatedEntities} authored entities and excluded " +
                  $"{report.ExcludedRoots} transient roots."
                : string.Empty;
            Log.Info($"Autosaved {name} ({uid}) to {path}." + snapshot +
                     $" Next save in {ReadableTimeLeft(uid)} seconds.");
        }
    }

    private TimeSpan CalculateNextTime()
    {
        return _timing.RealTime + TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.AutosaveInterval));
    }

    private double ReadableTimeLeft(EntityUid uid)
    {
        return Math.Round(_currentlyAutosaving[uid].next.TotalSeconds - _timing.RealTime.TotalSeconds);
    }

    #region Public API

    internal bool IsAutosaving(EntityUid uid)
    {
        return _currentlyAutosaving.ContainsKey(uid);
    }

    /// <summary>
    /// Executes the same persistence path used by the timer. Initialized maps
    /// use a validated mapper snapshot; initialized grid-only saves are refused.
    /// </summary>
    internal bool TryAutosaveNow(
        EntityUid uid,
        string originalFileName,
        out ResPath savedPath,
        out MappingSnapshotReport report,
        out string error)
    {
        savedPath = default;
        report = default;
        error = string.Empty;

        if (Deleted(uid))
        {
            error = $"Entity {uid} no longer exists.";
            return false;
        }

        var isMap = HasComp<MapComponent>(uid);
        var isGrid = HasComp<MapGridComponent>(uid);
        if (!isMap && !isGrid)
        {
            error = $"{ToPrettyString(uid)} is neither a map nor a grid.";
            return false;
        }

        if (LifeStage(uid) >= EntityLifeStage.MapInitialized && !isMap)
        {
            error = "Initialized autosave requires the complete map root; grid-only snapshots are unsupported.";
            return false;
        }

        var name = Path.GetFileName(originalFileName);
        if (string.IsNullOrWhiteSpace(name) || !ResPath.IsValidFilename(name))
        {
            error = $"'{originalFileName}' is not a valid autosave name.";
            return false;
        }

        try
        {
            var saveDirText = Path.Combine(_cfg.GetCVar(CCVars.AutosaveDirectory), name)
                .Replace(Path.DirectorySeparatorChar, '/');
            var saveDir = new ResPath(saveDirText).ToRootedPath();
            _resMan.UserData.CreateDir(saveDir);

            var destination = GetAvailableAutosavePath(_resMan.UserData, saveDir, DateTime.Now);
            if (LifeStage(uid) >= EntityLifeStage.MapInitialized)
            {
                if (!_snapshots.TryCreateMapSnapshotText(uid, out var yaml, out report, out error))
                    return false;

                var encoded = AutosaveEncoding.GetBytes(yaml);
                if (!MappingAutosaveFileWriter.TryWrite(
                        _resMan.UserData,
                        destination,
                        encoded,
                        out error))
                {
                    return false;
                }
            }
            else
            {
                var success = isMap
                    ? _loader.TrySaveMap(uid, destination)
                    : _loader.TrySaveGrid(uid, destination);
                if (!success)
                {
                    _resMan.UserData.Delete(destination);
                    error = $"The legacy serializer failed to save {ToPrettyString(uid)}.";
                    return false;
                }
            }

            savedPath = destination;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    internal static ResPath GetAvailableAutosavePath(
        IWritableDirProvider directory,
        ResPath saveDir,
        DateTime timestamp)
    {
        var stem = $"{timestamp:yyyy-MM-dd_HH.mm.ss.fff}-AUTO";
        for (var suffix = 0; suffix < 10_000; suffix++)
        {
            var name = suffix == 0 ? $"{stem}.yml" : $"{stem}-{suffix}.yml";
            var candidate = saveDir / name;
            if (!directory.Exists(candidate))
                return candidate;
        }

        throw new IOException($"No free autosave filename was available in '{saveDir}'.");
    }

    public void ToggleAutosave(MapId map, string? path = null)
    {
        if (_map.TryGetMap(map, out var uid))
            ToggleAutosave(uid.Value, path);
    }

    public void ToggleAutosave(EntityUid uid, string? path=null)
    {
        if (!_autosaveEnabled)
            return;

        if (_currentlyAutosaving.Remove(uid) || path == null)
            return;

        if (!HasComp<MapComponent>(uid) && !HasComp<MapGridComponent>(uid))
        {
            Log.Error($"{ToPrettyString(uid)} is neither a grid or map");
            return;
        }

        if (LifeStage(uid) >= EntityLifeStage.MapInitialized && !HasComp<MapComponent>(uid))
        {
            Log.Warning("Tried to enable initialized autosaving for a grid without its map root.");
            return;
        }

        var name = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(name) || !ResPath.IsValidFilename(name))
        {
            Log.Error($"'{path}' is not a valid autosave name.");
            return;
        }

        _currentlyAutosaving[uid] = (CalculateNextTime(), name);
        Log.Info($"Started autosaving map {path} ({uid}). Next save in {ReadableTimeLeft(uid)} seconds.");
    }

    #endregion

    #region Commands

    [AdminCommand(AdminFlags.Server | AdminFlags.Mapping)]
    private void ToggleAutosaveCommand(IConsoleShell shell, string argstr, string[] args)
    {
        if (args.Length != 1 && args.Length != 2)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!int.TryParse(args[0], out var intMapId))
        {
            shell.WriteError(Loc.GetString("cmd-mapping-failure-integer", ("arg", args[0])));
            return;
        }

        string? path = null;
        if (args.Length == 2)
        {
            path = args[1];
        }

        var mapId = new MapId(intMapId);
        ToggleAutosave(mapId, path);
    }

    #endregion
}
