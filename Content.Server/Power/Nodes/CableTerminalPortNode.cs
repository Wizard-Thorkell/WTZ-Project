using Content.Server.NodeContainer.Nodes;
using Content.Shared.NodeContainer;
using Robust.Shared.Map.Components;

namespace Content.Server.Power.Nodes
{
    [DataDefinition]
    public sealed partial class CableTerminalPortNode : Node
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

            var nodes = NodeHelpers.GetCardinalNeighborNodesOnZLevel(
                nodeQuery,
                xformQuery,
                gridEnt,
                gridIndex,
                zLevel,
                mapSystem,
                transformSystem,
                entMan,
                includeSameTile: false);
            foreach (var (dir, node) in nodes)
            {
                if (node is CableTerminalNode
                    && dir != Direction.Invalid
                    && xformQuery.GetComponent(node.Owner).LocalRotation.GetCardinalDir().GetOpposite() == dir)
                    yield return node;
            }
        }
    }
}
