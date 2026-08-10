using Content.Server.Atmos.EntitySystems;
using Content.Server.Anomaly.Components;
using Content.Shared.Anomaly.Components;

namespace Content.Server.Anomaly.Effects;

/// <summary>
/// This handles <see cref="TempAffectingAnomalyComponent"/>
/// </summary>
public sealed class TempAffectingAnomalySystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<TempAffectingAnomalyComponent, AnomalyComponent, TransformComponent>();
        while (query.MoveNext(out var ent, out var comp, out var anom, out var xform))
        {
            var mixture = _atmosphere.GetTileMixture((ent, xform), true);

            if (mixture is { })
            {
                mixture.Temperature += comp.TempChangePerSecond * anom.Severity * frameTime;
            }

            if (xform.GridUid != null && anom.Severity > comp.AnomalyHotSpotThreshold)
            {
                _atmosphere.HotspotExpose((ent, xform), comp.HotspotExposeTemperature, comp.HotspotExposeVolume, ent, true);
            }
        }
    }
}
