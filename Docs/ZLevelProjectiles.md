# WTZ Z-Aware Projectile Lifecycle And Vertical Trajectories

Physical projectiles and thrown entities retain the existing Robust horizontal
physics model. WTZ adds explicit floor authority at lifecycle boundaries without
turning ordinary same-floor shots or throws into vertical arcs. An explicit,
bounded trajectory is added only when a normal server-side fire or throw input
resolves a visible target entity on another floor of the same grid frame.

## Floor Authority

`SharedGunSystem.ShootProjectile` selects its source with the existing
`user ?? gun` rule. When that selected entity is valid, it stamps the projectile
with the source's authoritative world Z before enabling physics flight. A call
without a valid selected source keeps the world Z authored by its caller rather
than silently moving the projectile to local or world floor zero.

`ThrowingSystem.TryThrow` applies the same rule when a valid thrower is present.
Programmatic throws without a user preserve their existing authored floor.
While `ThrownItemComponent` is active, WTZ deliberately preserves Robust's
horizontal throw timing and distance; normal Z-level gravity resumes after the
item lands or stops being thrown.

Physics collision filtering compares effective world Z. A fired projectile can
therefore overlap a fixture on another floor without hitting it, including on a
grid whose local Z is displaced by `ZLevelFrameComponent.Origin`.

## Bounded Vertical Trajectories

`SharedZLevelBallisticSystem.TryStartTrajectory` receives the projectile or
thrown entity, the server-resolved target entity, and the actual planar
displacement after range clamping, recoil, or spread. It rejects same-floor,
hidden, cross-map, cross-grid, inactive, non-physical, non-finite, and trace
budget-exhausted requests. The target selects the destination floor, while the
displacement selects the planar route. Target movement after launch does not
retarget the shot.

The networked `ZLevelBallisticTrajectoryComponent` stores a route in the grid's
local frame. A line between floor centers crosses each half-level plane in
order, so a route from local Z 2 to Z 0 changes floors at one quarter and three
quarters of its planar distance. Before launch, a buffered `Projectile` trace
validates the coordinate frame and bounded crossing count. Each crossing is
then revalidated against the current boundary at the entity's actual tile,
allowing mapping changes made during flight to take effect.

The physics controller clips a substep at the next half-level crossing. After
the solver runs, WTZ restores only a collinear, non-reversed velocity response,
flushes contacts created by the clipped movement, and changes floor only if no
hard collision occurred. Reflections and source-floor obstacles therefore win
before a floor switch. Closed boundaries raise
`ZLevelBallisticBoundaryHitEvent`; projectiles become spent and thrown items
stop using their established lifecycle APIs.

`SharedPhysicsSystem.FlushPendingContacts` is the paired WTZ Engine primitive.
It performs one global broadphase/contact pass for every substep containing at
least one crossing, not one pass per projectile. The trajectory system reuses a
trace buffer and a crossing list. Metrics expose attempts, starts, completions,
crossings, closed boundaries, collision and invalid cancellations, and contact
flushes through `zlevelmetrics` and `ZLevelDebugOverlay`.

Thrown trajectories extend `ThrownItemComponent.LandTime` only when the
remaining route needs it. This preserves the normal throw hit event at the
destination instead of allowing the item to land between floors. Active
projectiles and throws temporarily suspend ordinary Z gravity; the existing
gravity solver resumes as soon as their ballistic lifecycle ends.

## Impacts And Embedding

Networked projectile impact effects carry the projectile's authoritative world
Z. The client stamps the spawned effect into its current grid frame, preventing
an upper-floor impact sprite from appearing on the base floor.

An embedded projectile becomes a child of the entity it hit and clears its own
explicit `ZLevelPositionComponent`. It then inherits every floor change made by
its parent. Detaching captures the inherited world Z before reparenting and
stamps that value into the destination grid or map frame afterward.

## Current Limits

- Vertical physical flight currently requires a target entity. Clicking an
  empty lower-floor tile does not yet carry enough authoritative view-Z input to
  select a destination floor.
- Player-facing visibility currently authorizes lower-floor targets. Upward
  physical targeting remains disabled until upper-floor FOV and input policy
  are implemented coherently.
- Routes remain inside one grid frame. Leaving that frame cancels the vertical
  route while ordinary Robust projectile motion continues.
- Translation after launch and initially rotated frames are supported. WTZ does
  not curve inertial velocity when a grid rotates during flight; a sufficiently
  strong rotation can make the shot leave its authored frame and cancel.
- Reflections retain the floor on which their physical collision occurred.
- Exact impact-effect appearance still needs a manual in-game visual pass; the
  event serializer and floor payload have automated client/server coverage.
- Cross-grid vertical flight is not inferred from overlapping XY geometry.

## Verification

Focused lifecycle and affected regressions:

```powershell
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --no-build --filter "FullyQualifiedName~ZLevelProjectileLifecycleTest|FullyQualifiedName~WeaponTests|FullyQualifiedName~EmbedTest|FullyQualifiedName~ItemThrowingTest"
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --no-build --filter "FullyQualifiedName~ZLevelBallisticTrajectoryTest"
```

P2.2a covers source-authoritative firing, source-less authored floors, throws
with and without users, actual same-XY cross-floor physics isolation, same-floor
impact, inherited embedding, and detach into a displaced frame. The completion
gate passed 5/5 lifecycle cases, 12/12 focused lifecycle and legacy regressions,
83/83 Z-level integration tests, 2/2 structural unit tests, and all three stress
baselines.

P2.2b covers physical projectile and thrown-item traversal through open and
closed boundaries, clipped source-floor contacts, reflection, normal gun and
manual throw consumers, range clamping, projectile spread, translated and
rotated frames, destination-floor reconciliation, authored traversal channels,
terminal metrics, and batched contact flushing. Its completion gate passes
18/18 trajectory cases, 12/12 affected regressions, the cumulative 101/101
Z-level matrix, 7/7 engine contact tests, 2/2 structural unit tests, and all
three stress baselines.
