using Content.Server.Stack;
using Content.Shared.Construction;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Prototypes;
using Content.Shared.Stacks;
using Content.Shared.ZLevel.Systems;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Server.Construction.Completions;

[UsedImplicitly]
[DataDefinition]
public sealed partial class GivePrototype : IGraphAction
{
    [DataField]
    public EntProtoId Prototype { get; private set; } = string.Empty;

    [DataField]
    public int Amount { get; private set; } = 1;

    public void PerformAction(EntityUid uid, EntityUid? userUid, IEntityManager entityManager)
    {
        if (string.IsNullOrEmpty(Prototype))
            return;

        var source = userUid ?? uid;
        var zLevels = entityManager.System<SharedZLevelSystem>();
        var worldZLevel = zLevels.GetWorldZLevel(source);

        if (EntityPrototypeHelpers.HasComponent<StackComponent>(Prototype))
        {
            var stackSystem = entityManager.EntitySysManager.GetEntitySystem<StackSystem>();
            var stacks = stackSystem.SpawnMultipleNextToOrDrop(Prototype, Amount, source);

            if (userUid is null || !entityManager.TryGetComponent(userUid, out HandsComponent? handsComp))
                return;

            foreach (var item in stacks)
            {
                stackSystem.TryMergeToHands(item, (userUid.Value, handsComp));
            }
        }
        else
        {
            var handsSystem = entityManager.EntitySysManager.GetEntitySystem<SharedHandsSystem>();
            var handsComp = userUid is not null ? entityManager.GetComponent<HandsComponent>(userUid.Value) : null;
            for (var i = 0; i < Amount; i++)
            {
                var item = entityManager.SpawnNextToOrDrop(Prototype, source);
                zLevels.StampWorldZLevelPosition(item, worldZLevel);
                handsSystem.PickupOrDrop(userUid, item, handsComp: handsComp);
            }
        }
    }
}
