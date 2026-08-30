using Robust.Shared.GameStates;

namespace Content.Shared.Movement.Components;

/// <summary>
/// Added to someone using a jetpack for movement purposes
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class JetpackUserComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Jetpack;

    [DataField, AutoNetworkedField]
    public float WeightlessAcceleration;

    [DataField, AutoNetworkedField]
    public float WeightlessFriction;

    [DataField, AutoNetworkedField]
    public float WeightlessFrictionNoInput;

    [DataField, AutoNetworkedField]
    public float WeightlessModifier;

    /// <summary>
    /// Runtime ownership flags for native Z-level flight granted by this jetpack.
    /// They are deliberately not map-serialized.
    /// </summary>
    [AutoNetworkedField]
    public bool StartedZLevelFlight;

    [AutoNetworkedField]
    public bool GrantedZLevelFlight;

    [AutoNetworkedField]
    public bool GrantedZLevelFlightControls;
}
