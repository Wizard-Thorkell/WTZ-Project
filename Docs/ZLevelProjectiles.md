# WTZ Z-Aware Projectile Lifecycle

Physical projectiles and thrown entities retain the existing Robust horizontal
physics model. WTZ adds explicit floor authority at lifecycle boundaries without
turning ordinary same-floor shots or throws into vertical arcs.

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

## Impacts And Embedding

Networked projectile impact effects carry the projectile's authoritative world
Z. The client stamps the spawned effect into its current grid frame, preventing
an upper-floor impact sprite from appearing on the base floor.

An embedded projectile becomes a child of the entity it hit and clears its own
explicit `ZLevelPositionComponent`. It then inherits every floor change made by
its parent. Detaching captures the inherited world Z before reparenting and
stamps that value into the destination grid or map frame afterward.

## Current Limits

- Physical projectiles and thrown entities currently travel horizontally on one
  discrete floor. They do not yet follow a deliberate trajectory through a deck
  opening.
- The client can select a visible lower-floor target for ranged input because
  hitscan already supports that route. A physical projectile remains on the
  shooter's floor until P2.2b supplies bounded vertical trajectory state.
- Reflections retain the floor on which their physical collision occurred.
- Exact impact-effect appearance still needs a manual in-game visual pass; the
  event serializer and floor payload have automated client/server coverage.
- Cross-grid vertical flight is not inferred from overlapping XY geometry.

## Verification

Focused lifecycle and affected regressions:

```powershell
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --no-build --filter "FullyQualifiedName~ZLevelProjectileLifecycleTest|FullyQualifiedName~WeaponTests|FullyQualifiedName~EmbedTest|FullyQualifiedName~ItemThrowingTest"
```

P2.2a covers source-authoritative firing, source-less authored floors, throws
with and without users, actual same-XY cross-floor physics isolation, same-floor
impact, inherited embedding, and detach into a displaced frame. The completion
gate passed 5/5 lifecycle cases, 12/12 focused lifecycle and legacy regressions,
83/83 Z-level integration tests, 2/2 structural unit tests, and all three stress
baselines.
