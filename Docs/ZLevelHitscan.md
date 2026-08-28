# WTZ Z-Aware Hitscan

WTZ routes authoritative hitscan collision through `SharedZLevelTraceSystem`.
The trace primitive owns ordered geometry and vertical boundary crossings;
weapon systems continue to own target preference, damage, reflection, range,
logging, and presentation.

## Authoritative Flow

1. The client resolves the entity under the cursor with
   `VisibleCrossFloorRanged`. Same-floor entities remain valid. A lower-floor
   entity is valid only through a complete stack of open `Visibility`
   boundaries. Without an entity, the pointer may select the nearest visible,
   non-empty lower-floor surface.
2. The ordinary gun request carries an optional world Z with its planar
   coordinates. The server resolves entity layers from server state and
   independently validates targetless layers against the effective origin,
   map, frame, downward direction, and visibility. This validation runs before
   ammunition use and again before every burst follow-up.
3. `HitscanBasicRaycastSystem` takes the origin world Z from the authoritative
   shooter transform. A cross-floor target must be on the same map and grid
   frame, visible from the shooter, and within three-dimensional max range. An
   explicit target that was deleted, moved, hidden, raised, or otherwise became
   invalid fails terminally instead of becoming a targetless planar trace.
4. One caller-owned `ZLevelTraceBuffer` evaluates collision with the hitscan's
   existing mask and the `Projectile` boundary channel. The first closed deck
   terminates the result before geometry on the next floor is evaluated.
5. The established container and `RequireProjectileTargetComponent` selection
   rules choose one entity from the ordered trace hits. Existing damage,
   reflection, and follow-up systems consume the same
   `HitscanRaycastFiredEvent` contract as before.

Visibility authorization and projectile passage are intentionally independent.
An opening may reveal a lower target while a projectile-specific provider still
blocks the shot. The server checks both policies even if the client selected the
target legitimately.

## Coordinates And Range

Same-floor shots retain their normal two-dimensional max-distance ray and Z 0
behavior. Cross-floor shots use the target's server transform to select the
destination world Z, or use the validated targetless coordinate layer when no
entity was selected. For an explicit cross-floor entity, the gun also derives
the planar direction from that current server transform, so a forged companion
coordinate cannot redirect the ray. Recoil still applies after authoritative
aim is established. Range is measured in XYZ with one floor equal to one world
distance unit.

Vertical traces currently require shooter and target to share one structural
grid frame. That matches the ownership contract of `ZLevelTrace`; overlapping
or nearby grids are never inferred from two-dimensional overlap. Moving,
translated, and rotated grids are resolved from their current transform and
`ZLevelFrameComponent` origin when the shot executes.

Invalid maps, non-finite coordinates, unresolved frames, and exhausted trace
budgets fail without a hit. Invalid explicit entities, stale selected layers,
upward targets, visibility denial, and three-dimensional range failure do the
same. None fall back to an unfiltered 2D ray or consume ammunition through the
normal gun request.

## Presentation

Hitscan visuals are split along the ordered trace segments. Each networked
sprite carries its segment world Z, and the client stamps the spawned effect
entity into that floor's local frame. Muzzle presentation belongs to the first
segment and impact presentation to the segment where the selected hit or
boundary stop occurs.

A perfectly vertical beam has no planar travel sprite to stretch. Its impact
can still be presented on the destination or blocking floor. This is a
two-dimensional presentation limit, not a collision limit.

## Current Limits

- Normal rendering hides floors above the viewer. Ranged targeting therefore
  permits visible lower floors only; upward targeting remains deferred to the
  P3 lighting/FOV and presentation policy.
- Targetless cross-floor aiming requires a real non-empty surface. It skips
  sparse empty layers and cannot aim at an arbitrary invisible Z plane.
- One shot uses one structural grid frame. Entry into or exit from several
  moving grids is deferred until the trace contract can compose frame-owned
  boundaries.
- Reflections originate on the hit entity's world Z and continue as a same-floor
  2D ray because the existing reflection event has no vertical target.
- Visual payload construction is covered by client/server compilation and
  serializer-hash parity. A manual in-game visual pass remains required before
  public-server hardening.

## Performance Evidence

The gameplay system reuses one pre-sized trace buffer, so WTZ-owned output and
scratch lists retain their capacity between shots. The collision-enabled Debug
workload measured 512 warmed queries at 447,608 managed bytes total, or 874.23
bytes per query, and 5.759 ms total on the reference development machine. This
is comparison evidence rather than a release threshold. The remaining managed
allocation is inside the Robust physics enumeration; the P1 tile-only buffered
path remains allocation-free.

## Verification

```powershell
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --no-build --filter "FullyQualifiedName~ZLevelHitscanTest"
```

The focused matrix covers Z 0 selection parity, filtering colliders from other
floors, visible open-floor entity and targetless-coordinate hits,
projectile-specific closed boundaries,
visibility denial, upward-target rejection, XYZ range, target-only obstacles,
diagonal moving frames, vertical-crossing budget failure, and the
collision-enabled allocation capture.

P2.4d3c adds real client/server gun requests, per-shot burst revalidation, idle
aim cleanup, authoritative entity direction, and fail-closed deleted/stale
targets. The combined final matrix passes 51/51 hitscan/ballistic cases and the
full focused Z-level suite passes 182/182 with no skips.
