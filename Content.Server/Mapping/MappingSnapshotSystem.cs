using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Content.Shared.Follower.Components;
using Content.Shared.Mapping;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Serialization.Markdown.Mapping;

namespace Content.Server.Mapping;

/// <summary>
/// Produces a detached map representation suitable for mapper-authored files.
/// </summary>
public sealed class MappingSnapshotSystem : EntitySystem
{
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly SharedZLevelMapSystem _zLevelMaps = default!;

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
            if (component is not FollowerComponent && component is not FollowedComponent)
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

            snapshot = result.Node;
            report = new MappingSnapshotReport(
                playerRoots,
                mindRoots,
                explicitTransientRoots,
                excludedComponents.Count);
            return true;
        }
        catch (Exception exception)
        {
            Log.Error($"Failed to create mapping snapshot for {ToPrettyString(mapUid)}: {exception}");
            error = exception.Message;
            return false;
        }
    }

    private bool IsPersistentSnapshotEntity(EntityUid uid, EntityUid mapUid)
    {
        return GetExclusionReason(uid, mapUid, out _) == MappingSnapshotExclusionReason.None;
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

            if (!TryComp<TransformComponent>(uid, out var transform))
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
    int TransientComponents)
{
    public int ExcludedRoots => PlayerRoots + MindRoots + ExplicitTransientRoots;
}
