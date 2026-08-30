using System.Diagnostics;
using System.Numerics;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared.Weather;
using Robust.Client.Graphics;
using Robust.Shared.Map.Components;

namespace Content.Client.Overlays;

public sealed partial class StencilOverlay
{
    private void DrawWeather(
        in OverlayDrawArgs args,
        CachedResources res,
        HashSet<Entity<WeatherStatusEffectComponent, StatusEffectComponent>> weathers,
        Matrix3x2 invMatrix)
    {
        var worldHandle = args.WorldHandle;
        var mapId = args.MapId;
        var worldAABB = args.WorldAABB;
        var worldBounds = args.WorldBounds;
        var position = args.Viewport.Eye?.Position.Position ?? Vector2.Zero;
        var viewerWorldZ = args.Viewport.Eye?.WorldZLevel ?? 0;
        if (args.Viewport.Eye is { } eye &&
            _viewContext.TryGetViewContext(eye, null, out var view))
        {
            viewerWorldZ = view.WorldZLevel;
        }

        _weatherPresentation.BuildMask(_weather, mapId, worldAABB, viewerWorldZ);
        var renderStarted = Stopwatch.GetTimestamp();
        var renderedBatches = 0;
        var renderedRuns = 0;
        var drawCalls = 0;
        var failClosed = _weatherPresentation.MaskEntireViewport;

        // Cut out the irrelevant bits via stencil
        // This is why we don't just use parallax; we might want specific tiles to get drawn over
        // particularly for planet maps or stations.
        worldHandle.RenderInRenderTarget(res.Blep!,
            () =>
            {
                var xformQuery = _entManager.GetEntityQuery<TransformComponent>();
                if (failClosed)
                {
                    worldHandle.SetTransform(invMatrix);
                    worldHandle.DrawRect(worldAABB, Color.White);
                    drawCalls++;
                    return;
                }

                foreach (var batch in _weatherPresentation.Batches)
                {
                    if (!_entManager.TryGetComponent(batch.GridUid, out MapGridComponent? grid) ||
                        grid.Deleted)
                    {
                        failClosed = true;
                        worldHandle.SetTransform(invMatrix);
                        worldHandle.DrawRect(worldAABB, Color.White);
                        drawCalls++;
                        return;
                    }

                    var matrix = _transform.GetWorldMatrix(batch.GridUid, xformQuery);
                    var matty = Matrix3x2.Multiply(matrix, invMatrix);
                    worldHandle.SetTransform(matty);
                    for (var i = 0; i < batch.RunCount; i++)
                    {
                        var run = _weatherPresentation.Runs[batch.FirstRun + i];
                        worldHandle.DrawRect(run.LocalBounds, Color.White);
                        renderedRuns++;
                        drawCalls++;
                    }

                    renderedBatches++;
                }
            },
            Color.Transparent);
        _weatherPresentation.RecordMaskRender(
            renderStarted,
            renderedBatches,
            renderedRuns,
            drawCalls,
            failClosed);

        worldHandle.SetTransform(Matrix3x2.Identity);
        worldHandle.UseShader(_protoManager.Index(StencilMask).Instance());
        worldHandle.DrawTextureRect(res.Blep!.Texture, worldBounds);
        var curTime = _timing.RealTime;


        foreach (var (uid, weather, status) in weathers)
        {
            var alpha = _weather.GetWeatherPercent((uid, status));
            var sprite = _sprite.GetFrame(weather.Sprite, curTime);

            // Draw the rain
            worldHandle.UseShader(_protoManager.Index(StencilDraw).Instance());
            _parallax.DrawParallax(worldHandle,
                worldAABB,
                sprite,
                curTime,
                position,
                weather.Scrolling ?? Vector2.Zero,
                modulate: (weather.Color ?? Color.White).WithAlpha(alpha));
        }

        worldHandle.SetTransform(Matrix3x2.Identity);
        worldHandle.UseShader(null);
    }
}
