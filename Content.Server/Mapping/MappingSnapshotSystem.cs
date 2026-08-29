using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Content.Shared.Follower.Components;
using Content.Shared.Mapping;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Markdown.Mapping;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Content.Server.Mapping;

/// <summary>
/// Produces a detached map representation suitable for mapper-authored files.
/// </summary>
/// <remarks>
/// This API always produces <see cref="FileCategory.Map"/> data. An initialized
/// map may be used as the read-only source, but players, minds, sessions, and
/// transient round state are intentionally excluded. Live-round persistence
/// requires a separate save contract and must not be added as an option here.
/// </remarks>
public sealed class MappingSnapshotSystem : EntitySystem
{
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly SharedZLevelMapSystem _zLevelMaps = default!;

    /// <summary>
    /// Creates and formats a validated mapper-authored snapshot as canonical
    /// map YAML. This is shared by network saves and server-side autosaves.
    /// </summary>
    public bool TryCreateMapSnapshotText(
        EntityUid mapUid,
        [NotNullWhen(true)] out string? yaml,
        out MappingSnapshotReport report,
        out string error)
    {
        yaml = null;
        if (!TryCreateMapSnapshot(mapUid, out var snapshot, out report, out error))
            return false;

        try
        {
            var document = new YamlDocument(snapshot.ToYaml());
            var stream = new YamlStream { document };
            using var writer = new StringWriter();
            stream.Save(new YamlMappingFix(new Emitter(writer)), false);
            yaml = writer.ToString();
            return true;
        }
        catch (Exception exception)
        {
            Log.Error($"Failed to format mapping snapshot for {ToPrettyString(mapUid)}: {exception}");
            error = exception.Message;
            return false;
        }
    }

    public bool TryCreateMapSnapshot(
        EntityUid mapUid,
        [NotNullWhen(true)] out MappingDataNode? snapshot,
        out MappingSnapshotReport report,
        out string error)
    {
        snapshot = null;
        report = default;
        error = string.Empty;

        if (!HasComp<MapComponent>(mapUid))
        {
            error = $"Entity {mapUid} is not a map.";
            return false;
        }

        if (!_zLevelMaps.TryValidate(mapUid, out error, uid => IsPersistentSnapshotEntity(uid, mapUid)))
            return false;

        var excluded = new HashSet<EntityUid>();
        var excludedComponents = new HashSet<(EntityUid Uid, Type Type)>();
        var playerRoots = 0;
        var mindRoots = 0;
        var explicitTransientRoots = 0;

        bool Filter(Entity<MetaDataComponent> entity)
        {
            if (entity.Owner == mapUid)
                return true;

            var reason = GetExclusionReason(entity.Owner, mapUid, out var excludedRoot);
            if (reason == MappingSnapshotExclusionReason.None)
                return true;

            if (!excluded.Add(excludedRoot))
                return false;

            switch (reason)
            {
                case MappingSnapshotExclusionReason.Player:
                    playerRoots++;
                    break;
                case MappingSnapshotExclusionReason.Mind:
                    mindRoots++;
                    break;
                case MappingSnapshotExclusionReason.ExplicitTransient:
                    explicitTransientRoots++;
                    break;
            }

            return false;
        }

        bool FilterComponent(EntityUid uid, IComponent component)
        {
            if (IsPersistentSnapshotComponent(component))
                return true;

            excludedComponents.Add((uid, component.GetType()));
            return false;
        }

        try
        {
            var options = SerializationOptions.Default with
            {
                Category = FileCategory.Map,
                ExpectPreInit = false,
                MissingEntityBehaviour = MissingEntityBehaviour.Ignore,
                EntityFilter = Filter,
                ComponentFilter = FilterComponent,
                SuppressMapSerializationEvents = true,
            };
            var result = _mapLoader.SerializeEntitiesRecursive([mapUid], options);
            if (result.Category != FileCategory.Map)
            {
                error = $"Mapping snapshot produced unexpected category {result.Category}.";
                return false;
            }

            if (!TryNormalizeAndValidateSnapshot(
                    result.Node,
                    out snapshot,
                    out var normalizedReferences,
                    out var validatedEntities,
                    out error))
            {
                return false;
            }

            report = new MappingSnapshotReport(
                playerRoots,
                mindRoots,
                explicitTransientRoots,
                excludedComponents.Count,
                normalizedReferences,
                validatedEntities);
            return true;
        }
        catch (Exception exception)
        {
            Log.Error($"Failed to create mapping snapshot for {ToPrettyString(mapUid)}: {exception}");
            error = exception.Message;
            return false;
        }
    }

    private bool TryNormalizeAndValidateSnapshot(
        MappingDataNode rawSnapshot,
        [NotNullWhen(true)] out MappingDataNode? snapshot,
        out int normalizedReferences,
        out int validatedEntities,
        out string error)
    {
        snapshot = null;
        normalizedReferences = 0;
        validatedEntities = 0;
        error = string.Empty;

        LoadResult? normalization = null;
        try
        {
            if (!TryLoadDetachedSnapshot(rawSnapshot, "raw mapping snapshot", out normalization))
            {
                error = "The raw mapping snapshot could not be loaded for detached normalization.";
                return false;
            }

            if (!TryValidateLoadedSnapshot(normalization, requireValidReferences: false, out var mapUid, out error))
                return false;

            normalizedReferences = normalization.InvalidEntityReferences.Count;
            var options = CreateDetachedSerializationOptions(mapUid);
            var normalized = _mapLoader.SerializeEntitiesRecursive([mapUid], options);
            if (normalized.Category != FileCategory.Map)
            {
                error = $"Detached normalization produced unexpected category {normalized.Category}.";
                return false;
            }

            snapshot = normalized.Node;
        }
        finally
        {
            if (normalization != null)
                _mapLoader.Delete(normalization);
        }

        LoadResult? validation = null;
        try
        {
            if (!TryLoadDetachedSnapshot(snapshot, "normalized mapping snapshot", out validation))
            {
                error = "The normalized mapping snapshot could not be loaded for final validation.";
                snapshot = null;
                return false;
            }

            if (!TryValidateLoadedSnapshot(validation, requireValidReferences: true, out _, out error))
            {
                snapshot = null;
                return false;
            }

            validatedEntities = validation.Entities.Count;
            return true;
        }
        finally
        {
            if (validation != null)
                _mapLoader.Delete(validation);
        }
    }

    private bool TryLoadDetachedSnapshot(
        MappingDataNode snapshot,
        string source,
        [NotNullWhen(true)] out LoadResult? result)
    {
        var options = MapLoadOptions.Default;
        options.ExpectedCategory = FileCategory.Map;
        options.DeserializationOptions.PauseMaps = true;
        options.DeserializationOptions.LogInvalidEntities = false;
        // EntityDeserializer consumes component mappings while reading them.
        // Keep the snapshot reusable for validation, transfer, and later loads.
        return _mapLoader.TryLoadGeneric(snapshot.Copy(), source, out result, options);
    }

    private bool TryValidateLoadedSnapshot(
        LoadResult result,
        bool requireValidReferences,
        out EntityUid mapUid,
        out string error)
    {
        mapUid = EntityUid.Invalid;
        error = string.Empty;

        if (result.Category != FileCategory.Map)
        {
            error = $"Snapshot load produced unexpected category {result.Category}.";
            return false;
        }

        if (result.Maps.Count != 1)
        {
            error = $"A mapping snapshot must contain exactly one map; found {result.Maps.Count}.";
            return false;
        }

        if (result.Orphans.Count != 0 || result.NullspaceEntities.Count != 0)
        {
            error = "A mapping snapshot may not contain orphaned or nullspace entities " +
                    $"(orphans={result.Orphans.Count}, nullspace={result.NullspaceEntities.Count}).";
            return false;
        }

        if (requireValidReferences && result.InvalidEntityReferences.Count != 0)
        {
            var invalid = result.InvalidEntityReferences[0];
            error = $"The normalized mapping snapshot contains {result.InvalidEntityReferences.Count} invalid " +
                    $"entity reference(s); first source YAML UID={invalid.SourceYamlUid?.ToString() ?? "unknown"}, " +
                    $"component={invalid.Component ?? "unknown"}, value='{invalid.SerializedValue}'.";
            return false;
        }

        mapUid = result.Maps.Single().Owner;
        if (_zLevelMaps.TryValidate(mapUid, out var zLevelError))
            return true;

        error = $"Loaded mapping snapshot has invalid Z-level state: {zLevelError}";
        return false;
    }

    private SerializationOptions CreateDetachedSerializationOptions(EntityUid mapUid)
    {
        return SerializationOptions.Default with
        {
            Category = FileCategory.Map,
            ExpectPreInit = false,
            MissingEntityBehaviour = MissingEntityBehaviour.Ignore,
            EntityFilter = entity => IsPersistentSnapshotEntity(entity.Owner, mapUid),
            ComponentFilter = (_, component) => IsPersistentSnapshotComponent(component),
        };
    }

    /// <summary>
    /// Returns whether an entity belongs to the mapper-authored persistence
    /// boundary for the supplied map. Descendants of excluded roots are also
    /// excluded.
    /// </summary>
    public bool IsPersistentSnapshotEntity(EntityUid uid, EntityUid mapUid)
    {
        return GetExclusionReason(uid, mapUid, out _) == MappingSnapshotExclusionReason.None;
    }

    /// <summary>
    /// Returns whether a component belongs to mapper-authored state.
    /// </summary>
    public bool IsPersistentSnapshotComponent(IComponent component)
    {
        return component is not FollowerComponent && component is not FollowedComponent;
    }

    private MappingSnapshotExclusionReason GetExclusionReason(
        EntityUid uid,
        EntityUid mapUid,
        out EntityUid excludedRoot)
    {
        excludedRoot = EntityUid.Invalid;
        while (uid.IsValid() && uid != mapUid)
        {
            var reason = GetDirectExclusionReason(uid);
            if (reason != MappingSnapshotExclusionReason.None)
            {
                excludedRoot = uid;
                return reason;
            }

            if (!TryComp(uid, out TransformComponent? transform))
                break;

            uid = transform.ParentUid;
        }

        return MappingSnapshotExclusionReason.None;
    }

    private MappingSnapshotExclusionReason GetDirectExclusionReason(EntityUid uid)
    {
        if (HasComp<ActorComponent>(uid) ||
            TryComp<MindContainerComponent>(uid, out var container) &&
            (container.HasMind || container.Mind != null))
        {
            return MappingSnapshotExclusionReason.Player;
        }

        if (HasComp<MindComponent>(uid))
            return MappingSnapshotExclusionReason.Mind;

        return HasComp<MappingSnapshotTransientComponent>(uid)
            ? MappingSnapshotExclusionReason.ExplicitTransient
            : MappingSnapshotExclusionReason.None;
    }

    private enum MappingSnapshotExclusionReason : byte
    {
        None,
        Player,
        Mind,
        ExplicitTransient,
    }
}

public readonly record struct MappingSnapshotReport(
    int PlayerRoots,
    int MindRoots,
    int ExplicitTransientRoots,
    int TransientComponents,
    int NormalizedReferences,
    int ValidatedEntities)
{
    public int ExcludedRoots => PlayerRoots + MindRoots + ExplicitTransientRoots;
}
