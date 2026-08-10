using Content.Server.NodeContainer;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.NodeContainer;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.Power.Nodes
{
    [DataDefinition]
    public sealed partial class CableTerminalNode : CableDeviceNode
    {
        public override IEnumerable<Node> GetReachableNodes(
            Entity<TransformComponent> xform,
            EntityQuery<NodeContainerComponent> nodeQuery,
            EntityQuery<TransformComponent> xformQuery,
            Entity<MapGridComponent>? grid,
            IEntityManager entMan)
        {
            if (!xform.Comp.Anchored || grid is not { } gridEnt)
                yield break;

            var mapSystem = entMan.System<SharedMapSystem>();
            var transformSystem = entMan.System<SharedTransformSystem>();
            var gridIndex = mapSystem.TileIndicesFor(gridEnt, xform.Comp.Coordinates);
            var zLevel = NodeHelpers.GetZLevel(xform, transformSystem, entMan);

            var dir = xform.Comp.LocalRotation.GetDir();
            var targetIdx = gridIndex.Offset(dir);

            foreach (var node in NodeHelpers.GetNodesInTileOnZLevel(
                         nodeQuery,
                         xformQuery,
                         gridEnt,
                         targetIdx,
                         zLevel,
                         mapSystem,
                         transformSystem,
                         entMan))
            {
                if (node is CableTerminalPortNode)
                    yield return node;
            }

            foreach (var node in base.GetReachableNodes(xform, nodeQuery, xformQuery, grid, entMan))
            {
                yield return node;
            }
        }
    }
}
