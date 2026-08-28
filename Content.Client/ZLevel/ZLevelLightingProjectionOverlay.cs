// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Content.Client.Graphics;
using Content.Client.Light;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client.ZLevel;

/// <summary>
/// Adds aperture-clipped lower-floor point lights to Clyde's light target
/// before the active-floor FOV mask is applied.
/// </summary>
public sealed class ZLevelLightingProjectionOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> ProjectionShader = "ZLevelLightProjection";
    private static readonly ProtoId<ShaderPrototype> HardShadowShader = "ZLevelLightProjectionShadowHard";
    private static readonly ProtoId<ShaderPrototype> SoftShadowShader = "ZLevelLightProjectionShadowSoft";

    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;

    private readonly IEntityManager _entityManager;
    private readonly ZLevelLightingProjectionSystem _projection;
    private readonly SharedTransformSystem _transform;
    private readonly List<DrawVertexUV2DColor> _vertices = new();
    private readonly OverlayResourceCache<ShadowResources> _resources = new();
    private ShaderInstance? _shader;

    public override OverlaySpace Space => OverlaySpace.BeforeLighting;

    public ZLevelLightingProjectionOverlay(
        ZLevelLightingProjectionSystem projection,
        IEntityManager entityManager)
    {
        IoCManager.InjectDependencies(this);
        _projection = projection;
        _entityManager = entityManager;
        _transform = entityManager.System<SharedTransformSystem>();
        ZIndex = AfterLightTargetOverlay.ContentZIndex + 2;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var started = Stopwatch.GetTimestamp();
        var batchesDrawn = 0;
        var runsDrawn = 0;
        var verticesDrawn = 0;
        var drawCalls = 0;
        var shadowStats = default(LightShadowMapRenderStats);

        if (args.Viewport.Eye is not { } eye)
        {
            _projection.RecordRender(started, 0, 0, 0, 0, shadowStats);
            return;
        }

        _projection.BuildProjection(args.MapId, args.WorldAABB, eye.WorldZLevel);
        if (_projection.Batches.Count == 0)
        {
            _projection.RecordRender(started, 0, 0, 0, 0, shadowStats);
            return;
        }

        _shader ??= _prototypeManager.Index(ProjectionShader).Instance();
        ShadowResources? shadowResources = null;
        IRenderTexture? shadowAtlas = null;
        var shadowRequests = _projection.GetShadowRequests();
        if (!shadowRequests.IsEmpty)
        {
            shadowResources = _resources.GetForViewport(
                args.Viewport,
                static _ => new ShadowResources());
            shadowAtlas = shadowResources.EnsureAtlas(_clyde, shadowRequests.Length);
            shadowStats = _clyde.RenderLightShadowMap(
                shadowAtlas,
                args.Viewport,
                args.MapId,
                shadowRequests);
        }

        var softShadows = _configuration.GetCVar(CVars.LightSoftShadows);
        var handle = args.WorldHandle;

        foreach (var batch in _projection.Batches)
        {
            if (!_entityManager.TryGetComponent(batch.GridUid, out MapGridComponent? grid) || grid.Deleted)
                continue;

            var worldMatrix = _transform.GetWorldMatrix(batch.GridUid);
            _vertices.Clear();
            ZLevelLightingProjectionGeometry.AppendBatchVertices(
                _vertices,
                _projection.Runs,
                batch,
                grid.TileSize,
                worldMatrix);
            if (_vertices.Count == 0)
                continue;

            var shader = _shader!;
            if (batch.HasShadow && shadowResources != null && shadowAtlas != null)
            {
                shader = shadowResources.GetShader(
                    _prototypeManager,
                    softShadows,
                    batch.ShadowRow);
                shader.SetParameter("lightCenter", batch.Emitter.WorldPosition);
                shader.SetParameter(
                    "lightIndex",
                    (batch.ShadowRow + 0.5f) / shadowAtlas.Size.Y);
                shader.SetParameter("shadowMap", shadowAtlas.Texture);
                if (softShadows)
                    shader.SetParameter("lightSoftness", batch.Emitter.Softness);
            }

            handle.SetTransform(worldMatrix);
            handle.UseShader(shader);
            handle.DrawPrimitives(
                DrawPrimitiveTopology.TriangleList,
                ResolveMask(batch.Emitter.MaskPath),
                CollectionsMarshal.AsSpan(_vertices));

            batchesDrawn++;
            runsDrawn += batch.RunCount;
            verticesDrawn += _vertices.Count;
            drawCalls++;
        }

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
        _projection.RecordRender(
            started,
            batchesDrawn,
            runsDrawn,
            verticesDrawn,
            drawCalls,
            shadowStats);
    }

    private Texture ResolveMask(string? maskPath)
    {
        return maskPath != null &&
               _resourceCache.TryGetResource<TextureResource>(maskPath, out var resource)
            ? resource.Texture
            : Texture.White;
    }

    internal static int GetShadowAtlasCapacity(int requiredRows)
    {
        if (requiredRows <= 0 ||
            requiredRows > ZLevelLightingProjectionSystem.MaximumShadowLightsPerFrame)
        {
            throw new ArgumentOutOfRangeException(nameof(requiredRows));
        }

        var capacity = 1;
        while (capacity < requiredRows)
            capacity <<= 1;

        return capacity;
    }

    protected override void DisposeBehavior()
    {
        _resources.Dispose();
        base.DisposeBehavior();
    }

    private sealed class ShadowResources : IDisposable
    {
        private readonly List<ShaderInstance> _hardShaders = new();
        private readonly List<ShaderInstance> _softShaders = new();

        public IRenderTexture? Atlas { get; private set; }

        public IRenderTexture EnsureAtlas(IClyde clyde, int requiredRows)
        {
            if (requiredRows <= 0)
                throw new ArgumentOutOfRangeException(nameof(requiredRows));

            if (Atlas != null && Atlas.Size.Y >= requiredRows)
                return Atlas;

            Atlas?.Dispose();
            Atlas = clyde.CreateLightShadowMap(
                ZLevelLightingProjectionOverlay.GetShadowAtlasCapacity(requiredRows),
                "zlevel-projected-light-shadows");
            return Atlas;
        }

        public ShaderInstance GetShader(
            IPrototypeManager prototypes,
            bool soft,
            int row)
        {
            if (row < 0)
                throw new ArgumentOutOfRangeException(nameof(row));

            var shaders = soft ? _softShaders : _hardShaders;
            var prototype = soft ? SoftShadowShader : HardShadowShader;
            while (shaders.Count <= row)
                shaders.Add(prototypes.Index(prototype).InstanceUnique());

            return shaders[row];
        }

        public void Dispose()
        {
            Atlas?.Dispose();
            Atlas = null;
            DisposeShaders(_hardShaders);
            DisposeShaders(_softShaders);
        }

        private static void DisposeShaders(List<ShaderInstance> shaders)
        {
            foreach (var shader in shaders)
                shader.Dispose();

            shaders.Clear();
        }
    }
}

internal static class ZLevelLightingProjectionGeometry
{
    internal const float FalloffQuantization = 16f;
    internal const float CurveQuantization = 4095f;
    internal const float ParameterBase = 4096f;
    internal const float MaximumFalloff = 255f;

    public static int AppendBatchVertices(
        List<DrawVertexUV2DColor> vertices,
        IReadOnlyList<ZLevelLightProjectionRun> runs,
        in ZLevelLightProjectionBatch batch,
        float tileSize,
        in Matrix3x2 worldMatrix)
    {
        var initialCount = vertices.Count;
        if (batch.ProjectedRadius <= 0f || batch.Emitter.Radius <= 0f || tileSize <= 0f)
            return 0;

        var alpha = Math.Max(0f, batch.Emitter.Energy * batch.Transmission);
        var modulation = Color.FromSrgb(batch.Emitter.Color.WithAlpha(alpha));
        var verticalDistance = batch.Depth * ZLevelLightingProjectionSystem.VerticalDistancePerLevel;
        var heightTerm = (verticalDistance * verticalDistance +
                          ZLevelLightingProjectionSystem.NativeLightHeightSquared) /
                         (batch.Emitter.Radius * batch.Emitter.Radius);
        var packedParameters = PackAttenuationParameters(
            batch.Emitter.Falloff,
            batch.Emitter.CurveFactor);

        for (var i = 0; i < batch.RunCount; i++)
        {
            var run = runs[batch.FirstRun + i];
            if (run.GridUid != batch.GridUid)
                continue;

            var left = run.StartX * tileSize;
            var right = (run.EndX + 1) * tileSize;
            var bottom = run.Y * tileSize;
            var top = (run.Y + 1) * tileSize;

            var bottomLeft = CreateVertex(
                new Vector2(left, bottom), batch, worldMatrix, modulation, heightTerm, packedParameters);
            var bottomRight = CreateVertex(
                new Vector2(right, bottom), batch, worldMatrix, modulation, heightTerm, packedParameters);
            var topRight = CreateVertex(
                new Vector2(right, top), batch, worldMatrix, modulation, heightTerm, packedParameters);
            var topLeft = CreateVertex(
                new Vector2(left, top), batch, worldMatrix, modulation, heightTerm, packedParameters);

            vertices.Add(bottomLeft);
            vertices.Add(bottomRight);
            vertices.Add(topRight);
            vertices.Add(bottomLeft);
            vertices.Add(topRight);
            vertices.Add(topLeft);
        }

        return vertices.Count - initialCount;
    }

    internal static float PackAttenuationParameters(float falloff, float curveFactor)
    {
        var falloffQuantized = MathF.Round(Math.Clamp(falloff, 0f, MaximumFalloff) * FalloffQuantization);
        var curveQuantized = MathF.Round(Math.Clamp(curveFactor, 0f, 1f) * CurveQuantization);
        return falloffQuantized * ParameterBase + curveQuantized;
    }

    internal static float UnpackFalloff(float packed)
    {
        return MathF.Floor((packed + 0.5f) / ParameterBase) / FalloffQuantization;
    }

    internal static float UnpackCurveFactor(float packed)
    {
        var falloffQuantized = MathF.Floor((packed + 0.5f) / ParameterBase);
        return (packed - falloffQuantized * ParameterBase) / CurveQuantization;
    }

    private static DrawVertexUV2DColor CreateVertex(
        Vector2 localPosition,
        in ZLevelLightProjectionBatch batch,
        in Matrix3x2 worldMatrix,
        Color modulation,
        float heightTerm,
        float packedParameters)
    {
        var worldPosition = Vector2.Transform(localPosition, worldMatrix);
        var lightLocalDelta = (-batch.Emitter.MaskRotation).RotateVec(
            worldPosition - batch.Emitter.WorldPosition);
        var diameter = batch.Emitter.Radius * 2f;
        var uv = new Vector2(
            lightLocalDelta.X / diameter + 0.5f,
            0.5f - lightLocalDelta.Y / diameter);

        return new DrawVertexUV2DColor(localPosition, uv, modulation)
        {
            UV2 = new Vector2(heightTerm, packedParameters),
        };
    }
}
