using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Gravity;
using Content.Shared.ZLevel;

namespace Content.Server.Gravity;

public sealed class GravityGeneratorSystem : SharedGravityGeneratorSystem
{
    [Dependency] private readonly GravitySystem _gravitySystem = default!;
    [Dependency] private readonly SharedPointLightSystem _lights = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GravityGeneratorComponent, EntParentChangedMessage>(OnParentChanged);
        SubscribeLocalEvent<GravityGeneratorComponent, ChargedMachineActivatedEvent>(OnActivated);
        SubscribeLocalEvent<GravityGeneratorComponent, ChargedMachineDeactivatedEvent>(OnDeactivated);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<GravityGeneratorComponent, PowerChargeComponent>();
        while (query.MoveNext(out var uid, out var grav, out var charge))
        {
            if (!_lights.TryGetLight(uid, out var pointLight))
                continue;

            _lights.SetEnabled(uid, charge.Charge > 0, pointLight);
            _lights.SetRadius(uid, MathHelper.Lerp(grav.LightRadiusMin, grav.LightRadiusMax, charge.Charge),
                pointLight);
        }
    }

    private void OnActivated(Entity<GravityGeneratorComponent> ent, ref ChargedMachineActivatedEvent args)
    {
        ent.Comp.GravityActive = true;
        Dirty(ent, ent.Comp);

        var xform = Transform(ent);
        RaiseSourceChanged(ent, null);

        if (TryComp(xform.ParentUid, out GravityComponent? gravity))
        {
            _gravitySystem.EnableGravity(xform.ParentUid, gravity);
        }
    }

    private void OnDeactivated(Entity<GravityGeneratorComponent> ent, ref ChargedMachineDeactivatedEvent args)
    {
        ent.Comp.GravityActive = false;
        Dirty(ent, ent.Comp);

        var xform = Transform(ent);
        RaiseSourceChanged(ent, null);

        if (TryComp(xform.ParentUid, out GravityComponent? gravity))
        {
            _gravitySystem.RefreshGravity(xform.ParentUid, gravity);
        }
    }

    private void OnParentChanged(EntityUid uid, GravityGeneratorComponent component, ref EntParentChangedMessage args)
    {
        if (component.GravityActive && TryComp(args.OldParent, out GravityComponent? gravity))
        {
            _gravitySystem.RefreshGravity(args.OldParent.Value, gravity);
        }

        if (component.GravityActive && TryComp(args.Transform.ParentUid, out gravity))
            _gravitySystem.EnableGravity(args.Transform.ParentUid, gravity);

        var ev = new ZLevelGravitySourceChangedEvent(args.OldParent);
        RaiseLocalEvent(uid, ref ev);
    }

    private void RaiseSourceChanged(Entity<GravityGeneratorComponent> ent, EntityUid? oldGridUid)
    {
        var ev = new ZLevelGravitySourceChangedEvent(oldGridUid);
        RaiseLocalEvent(ent.Owner, ref ev);
    }
}
