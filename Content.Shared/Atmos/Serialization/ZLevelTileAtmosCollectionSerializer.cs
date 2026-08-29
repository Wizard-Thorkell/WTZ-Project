using System.Globalization;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Generic;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;
using Robust.Shared.Utility;

namespace Content.Shared.Atmos.Serialization;

/// <summary>
/// Serializes persistent atmosphere mixtures for sparse, non-zero grid layers.
/// Runtime adjacency cells and hotspot state are deliberately reconstructed after load.
/// </summary>
public sealed partial class ZLevelTileAtmosCollectionSerializer :
    ITypeSerializer<Dictionary<ZLevelTileIndices, TileAtmosphere>, MappingDataNode>,
    ITypeCopier<Dictionary<ZLevelTileIndices, TileAtmosphere>>
{
    private const int CurrentVersion = 1;
    private const int ChunkSize = 4;

    public ValidationNode Validate(
        ISerializationManager serializationManager,
        MappingDataNode node,
        IDependencyCollection dependencies,
        ISerializationContext? context = null)
    {
        if (node.Count == 0)
            return new ValidatedMappingNode(new Dictionary<ValidationNode, ValidationNode>());

        if (node.TryGet("version", out var versionNode) &&
            versionNode is ValueDataNode valueNode &&
            int.TryParse(valueNode.Value, CultureInfo.InvariantCulture, out var version) &&
            version != CurrentVersion)
        {
            return new ErrorNode(versionNode, $"Unsupported Z-level atmosphere serialization version {version}.");
        }

        return serializationManager.ValidateNode<ZLevelTileAtmosEnvelope>(node, context);
    }

    public Dictionary<ZLevelTileIndices, TileAtmosphere> Read(
        ISerializationManager serializationManager,
        MappingDataNode node,
        IDependencyCollection dependencies,
        SerializationHookContext hookCtx,
        ISerializationContext? context = null,
        ISerializationManager.InstantiationDelegate<Dictionary<ZLevelTileIndices, TileAtmosphere>>? instanceProvider = null)
    {
        var tiles = new Dictionary<ZLevelTileIndices, TileAtmosphere>();
        if (node.Count == 0)
            return tiles;

        if (!node.TryGet("version", out var versionNode) || versionNode is not ValueDataNode valueNode)
            throw new InvalidOperationException("Z-level atmosphere serialization is missing its version.");

        var version = valueNode.AsInt();
        if (version != CurrentVersion)
            throw new InvalidOperationException($"Unsupported Z-level atmosphere serialization version {version}.");

        if (!node.TryGet("data", out var rawDataNode))
            throw new InvalidOperationException("Z-level atmosphere serialization is missing its data.");

        var dataNode = (MappingDataNode) rawDataNode;
        var chunkSize = serializationManager.Read<int>(dataNode["chunkSize"], hookCtx, context);
        if (chunkSize != ChunkSize)
            throw new InvalidOperationException($"Unsupported Z-level atmosphere chunk size {chunkSize}.");

        dataNode.TryGet("uniqueMixes", out var mixNode);
        dataNode.TryGet("layers", out var layerNode);
        var unique = mixNode == null
            ? null
            : serializationManager.Read<List<GasMixture>?>(mixNode, hookCtx, context);
        var layers = layerNode == null
            ? null
            : serializationManager.Read<Dictionary<int, Dictionary<Vector2i, ZLevelTileAtmosChunk>>?>(
                layerNode,
                hookCtx,
                context);

        if (unique == null || layers == null)
            return tiles;

        foreach (var (z, chunks) in layers)
        {
            foreach (var (chunkOrigin, chunk) in chunks)
            {
                foreach (var (mix, flags) in chunk.Data)
                {
                    for (var x = 0; x < chunkSize; x++)
                    {
                        for (var y = 0; y < chunkSize; y++)
                        {
                            if ((flags & (1U << (x + y * chunkSize))) == 0)
                                continue;

                            var indices = new ZLevelTileIndices(
                                x + chunkOrigin.X * chunkSize,
                                y + chunkOrigin.Y * chunkSize,
                                z);
                            try
                            {
                                tiles.Add(indices, new TileAtmosphere(
                                    EntityUid.Invalid,
                                    new Vector2i(indices.X, indices.Y),
                                    unique[mix].Clone())
                                {
                                    ZLevel = z,
                                });
                            }
                            catch (ArgumentOutOfRangeException)
                            {
                                var sawmill = dependencies.Resolve<ILogManager>().GetSawmill("szr");
                                sawmill.Error(
                                    $"Error during Z-level atmos serialization! Tile at {indices} points to an unique mix ({mix}) out of range!");
                            }
                        }
                    }
                }
            }
        }

        return tiles;
    }

    public DataNode Write(
        ISerializationManager serializationManager,
        Dictionary<ZLevelTileIndices, TileAtmosphere> value,
        IDependencyCollection dependencies,
        bool alwaysWrite = false,
        ISerializationContext? context = null)
    {
        var uniqueMixes = new List<GasMixture>();
        var layers = new Dictionary<int, Dictionary<Vector2i, ZLevelTileAtmosChunk>>();

        foreach (var (gridIndices, tile) in value)
        {
            if (tile.Air == null || tile.NoGridTile)
                continue;

            var mixIndex = uniqueMixes.IndexOf(tile.Air);
            if (mixIndex == -1)
            {
                mixIndex = uniqueMixes.Count;
                uniqueMixes.Add(tile.Air);
            }

            var chunks = layers.GetOrNew(gridIndices.Z);
            var xy = new Vector2i(gridIndices.X, gridIndices.Y);
            var chunkOrigin = SharedMapSystem.GetChunkIndices(xy, ChunkSize);
            var tileChunk = chunks.GetOrNew(chunkOrigin);
            var indices = SharedMapSystem.GetChunkRelative(xy, ChunkSize);
            var mixFlags = tileChunk.Data.GetOrNew(mixIndex);
            mixFlags |= 1U << (indices.X + indices.Y * ChunkSize);
            tileChunk.Data[mixIndex] = mixFlags;
        }

        return new MappingDataNode
        {
            { "version", CurrentVersion.ToString(CultureInfo.InvariantCulture) },
            {
                "data", serializationManager.WriteValue(new ZLevelTileAtmosData
                {
                    ChunkSize = ChunkSize,
                    UniqueMixes = uniqueMixes.Count == 0 ? null : uniqueMixes,
                    Layers = layers.Count == 0 ? null : layers,
                }, alwaysWrite, context)
            },
        };
    }

    public void CopyTo(
        ISerializationManager serializationManager,
        Dictionary<ZLevelTileIndices, TileAtmosphere> source,
        ref Dictionary<ZLevelTileIndices, TileAtmosphere> target,
        IDependencyCollection dependencies,
        SerializationHookContext hookCtx,
        ISerializationContext? context = null)
    {
        target.Clear();
        foreach (var (key, value) in source)
            target.Add(key, new TileAtmosphere(value));
    }

    [DataDefinition]
    private partial struct ZLevelTileAtmosEnvelope
    {
        [DataField("version", required: true)]
        public int Version;

        [DataField("data", required: true)]
        public ZLevelTileAtmosData Data;
    }

    [DataDefinition]
    private partial struct ZLevelTileAtmosData
    {
        [DataField("chunkSize", required: true)]
        public int ChunkSize;

        [DataField("uniqueMixes")]
        public List<GasMixture>? UniqueMixes;

        [DataField("layers")]
        public Dictionary<int, Dictionary<Vector2i, ZLevelTileAtmosChunk>>? Layers;
    }

    [DataDefinition]
    private partial record struct ZLevelTileAtmosChunk()
    {
        [IncludeDataField(customTypeSerializer: typeof(DictionarySerializer<int, uint>))]
        public Dictionary<int, uint> Data = new();
    }
}
