using Content.Shared.StatusEffectNew.Components;
using Content.Shared.Weather;
using Robust.Client.Audio;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Client.Weather;

public sealed class WeatherSystem : SharedWeatherSystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ZLevelWeatherPresentationSystem _presentation = default!;

    [Dependency] private readonly EntityQuery<AudioComponent> _audioQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WeatherStatusEffectComponent, ComponentShutdown>(OnComponentShutdown);
    }

    private void OnComponentShutdown(Entity<WeatherStatusEffectComponent> ent, ref ComponentShutdown args)
    {
        ent.Comp.Stream = _audio.Stop(ent.Comp.Stream);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!Timing.IsFirstTimePredicted)
            return;

        var player = _playerManager.LocalEntity;

        if (player == null)
            return;

        var playerXform = Transform(player.Value);
        ZLevelWeatherAudioExposure? exposure = null;

        var query = EntityQueryEnumerator<WeatherStatusEffectComponent, StatusEffectComponent>();
        while (query.MoveNext(out var uid, out var weather, out var status))
        {
            if (weather.Sound == null || status.AppliedTo != playerXform.MapUid)
            {
                weather.Stream = _audio.Stop(weather.Stream);
                continue;
            }

            weather.Stream ??= _audio.PlayGlobal(weather.Sound, Filter.Local(), true)?.Entity;

            if (!_audioQuery.TryComp(weather.Stream, out var audio))
                continue;

            exposure ??= _presentation.FindAudioExposure(this, player.Value);
            var occlusion = exposure.Value.Termination switch
            {
                ZLevelWeatherAudioTermination.Direct => 0f,
                ZLevelWeatherAudioTermination.Nearby => GetNearbyOcclusion(
                    playerXform,
                    exposure.Value.NearestExposedTile),
                _ => 3f,
            };

            var alpha = GetWeatherPercent((uid, status));
            alpha *= SharedAudioSystem.VolumeToGain(weather.Sound.Params.Volume);
            _audio.SetGain(weather.Stream, alpha, audio);
            audio.Occlusion = occlusion;
        }
    }

    private float GetNearbyOcclusion(
        TransformComponent playerTransform,
        EntityCoordinates? nearestNode)
    {
        if (nearestNode is not { } node)
            return 3f;

        var entityPosition = _transform.GetMapCoordinates(playerTransform);
        var nodePosition = _transform.ToMapCoordinates(node).Position;
        var delta = nodePosition - entityPosition.Position;
        return _audio.GetOcclusion(entityPosition, delta, delta.Length());
    }
}
