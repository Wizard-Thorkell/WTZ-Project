// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Numerics;
using Content.Client.ZLevel;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests.Helpers;
using Content.Server.Administration.Managers;
using Content.Server.Silicons.StationAi;
using Content.Server.Verbs;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Actions.Events;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Maps;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Verbs;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.GameObjects;
using Robust.Client.Input;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;

namespace Content.IntegrationTests.Tests.ZLevel;

public sealed partial class ZLevelInteractionAuthorityTest : GameTest
{
    private const int FrameOrigin = 5;
    private const int AllocationIterations = 4_096;

    [TestPrototypes]
    private const string InteractionPrototypes = @"
- type: entity
  parent: BaseStructure
  id: ZLevelInteractionObstacle
  components:
  - type: Physics
    bodyType: Static
  - type: Fixtures
    fixtures:
      interaction:
        shape:
          !type:PhysShapeAabb
          bounds: ""-0.1,-0.1,0.1,0.1""
        mask:
        - FullTileMask
        layer:
        - WallLayer
        hard: true
";

    [Test]
    public async Task ExplicitVerticalInteractionRequiresItsBoundaryChannel()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var lower = Spawn(testMap, new Vector2(0.5f, 0.5f), 0);
            var upper = Spawn(testMap, new Vector2(0.5f, 0.5f), 1);
            var authority = SEntMan.System<SharedZLevelInteractionSystem>();
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>();
            metrics.ResetCounters();

            Assert.Multiple(() =>
            {
                Assert.That(authority.CanDirectlyInteract(lower, lower), Is.True);
                Assert.That(authority.CanDirectlyInteract(lower, upper), Is.False);
                Assert.That(authority.CanInteractThroughOpenBoundary(lower, upper, 1f), Is.False);
            });

            var provider = SetBoundary(
                testMap,
                Vector2i.Zero,
                0,
                opens: ZLevelBoundaryChannels.Projectile);
            Assert.That(authority.CanInteractThroughOpenBoundary(lower, upper, 1f), Is.False);

            SetBoundary(
                provider,
                opens: ZLevelBoundaryChannels.Interaction,
                closes: ZLevelBoundaryChannels.None);
            Assert.Multiple(() =>
            {
                Assert.That(authority.CanDirectlyInteract(lower, upper), Is.False);
                Assert.That(authority.CanInteractThroughOpenBoundary(lower, upper, 0f), Is.False);
                Assert.That(authority.CanInteractThroughOpenBoundary(lower, upper, float.NaN), Is.False);
                Assert.That(authority.CanInteractThroughOpenBoundary(lower, upper, 0.9f), Is.False);
                Assert.That(authority.CanInteractThroughOpenBoundary(lower, upper, 1f), Is.True);
            });

            SetBoundary(
                provider,
                opens: ZLevelBoundaryChannels.All,
                closes: ZLevelBoundaryChannels.Interaction);
            Assert.That(authority.CanInteractThroughOpenBoundary(lower, upper, 1f), Is.False);

            var snapshot = metrics.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(snapshot.InteractionQueries, Is.EqualTo(10));
                Assert.That(snapshot.InteractionAllowed, Is.EqualTo(2));
                Assert.That(snapshot.InteractionRejected, Is.EqualTo(8));
                Assert.That(snapshot.InteractionSameLevelAllowed, Is.EqualTo(1));
                Assert.That(snapshot.InteractionVerticalAllowed, Is.EqualTo(1));
                Assert.That(snapshot.InteractionDifferentLevelRejected, Is.EqualTo(2));
                Assert.That(snapshot.InteractionRangeRejected, Is.EqualTo(3));
                Assert.That(snapshot.InteractionTraceRejected, Is.EqualTo(3));
                Assert.That(snapshot.InteractionRemoteOriginQueries, Is.Zero);
                Assert.That(snapshot.InteractionPhysicalQueries, Is.Zero);
                Assert.That(snapshot.TraceQueries, Is.EqualTo(4));
            });
        });
    }

    [Test]
    public async Task UseReachExtendsOnlyThroughOpenInteractionBoundaries()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var position = new Vector2(0.5f, 0.5f);
            var user = Spawn(testMap, position, 1);
            var target = Spawn(testMap, position, 0);
            var interaction = SEntMan.System<SharedInteractionSystem>();

            Assert.Multiple(() =>
            {
                Assert.That(interaction.InRangeUnobstructed(user, target), Is.False,
                    "The generic SS14 reach helper must remain same-floor only.");
                Assert.That(interaction.InRangeUnobstructedForUse(user, target), Is.False,
                    "A closed floor must reject the explicit vertical use helper.");
            });

            SetBoundary(
                testMap,
                Vector2i.Zero,
                0,
                opens: ZLevelBoundaryChannels.Interaction | ZLevelBoundaryChannels.Visibility);
            Assert.That(interaction.InRangeUnobstructedForUse(user, target), Is.True);
            Assert.That(interaction.InRangeUnobstructedForUse(target, user), Is.False,
                "Server authority must match the client rule that only visible lower floors are selectable.");

            var farTarget = Spawn(testMap, new Vector2(1.7f, 0.5f), 0);
            SetBoundary(
                testMap,
                new Vector2i(1, 0),
                0,
                opens: ZLevelBoundaryChannels.Interaction | ZLevelBoundaryChannels.Visibility);
            Assert.That(interaction.InRangeUnobstructedForUse(user, farTarget), Is.False,
                "Combined XY and discrete-Z distance must retain the normal 1.5 tile use range.");
        });
    }

    [Test]
    public async Task VisibilityOnlyGrateCannotAuthorizePhysicalUse()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var position = new Vector2(0.5f, 0.5f);
            var user = Spawn(testMap, position, 1);
            var target = Spawn(testMap, position, 0);
            var grate = SEntMan.SpawnEntity(
                "ZLevelGrateBoundaryMarker",
                new EntityCoordinates(testMap.Grid, position));
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(grate, 1), Is.True);

            var visibility = SEntMan.System<SharedZLevelVisibilitySystem>();
            var interaction = SEntMan.System<SharedInteractionSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(
                    visibility.IsEntityVisibleFrom(target, testMap.MapId, FrameOrigin + 1),
                    Is.True,
                    "A grate must keep the lower target visible.");
                Assert.That(
                    interaction.InRangeUnobstructedForUse(user, target),
                    Is.False,
                    "Visibility alone must never grant the Interaction channel.");
            });
        });
    }

    [Test]
    public async Task VerticalUseChecksObstructionsOnEveryTraceSegment()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var user = Spawn(testMap, new Vector2(0.1f, 0.5f), 1);
            var target = Spawn(testMap, new Vector2(1f, 0.5f), 0);
            SetBoundary(
                testMap,
                Vector2i.Zero,
                0,
                opens: ZLevelBoundaryChannels.Interaction | ZLevelBoundaryChannels.Visibility);
            var interaction = SEntMan.System<SharedInteractionSystem>();
            Assert.That(interaction.InRangeUnobstructedForUse(user, target), Is.True,
                "The rotated and translated frame must permit an unobstructed diagonal portal trace.");

            var upperBlocker = Spawn(
                testMap,
                "ZLevelInteractionObstacle",
                new Vector2(0.35f, 0.5f),
                1);
            Assert.That(interaction.InRangeUnobstructedForUse(user, target), Is.False,
                "A blocker before the vertical crossing must reject use.");
            SEntMan.DeleteEntity(upperBlocker);

            var lowerBlocker = Spawn(
                testMap,
                "ZLevelInteractionObstacle",
                new Vector2(0.8f, 0.5f),
                0);
            Assert.That(interaction.InRangeUnobstructedForUse(user, target), Is.False,
                "A blocker after the vertical crossing must reject use.");
            SEntMan.DeleteEntity(lowerBlocker);

            Assert.That(interaction.InRangeUnobstructedForUse(user, target), Is.True);
        });
    }

    [Test]
    public void InteractionTargetingAlwaysPrefersTheCurrentFloor()
    {
        var comparer = ZLevelTargetingSystem.InteractionClickableComparer.Instance;
        var sameFloor = (new EntityUid(1), 0, int.MinValue, 0u, float.MinValue);
        var lowerFloor = (new EntityUid(2), 1, int.MaxValue, uint.MaxValue, float.MaxValue);
        var twoFloorsDown = (new EntityUid(3), 2, int.MaxValue, uint.MaxValue, float.MaxValue);

        Assert.Multiple(() =>
        {
            Assert.That(comparer.Compare(sameFloor, lowerFloor), Is.LessThan(0),
                "Draw order must never let a lower-floor sprite steal a same-floor use click.");
            Assert.That(comparer.Compare(lowerFloor, twoFloorsDown), Is.LessThan(0),
                "After the current floor, the nearest visible lower floor must win.");
        });
    }

    [Test]
    public async Task PhysicalSameFloorCheckIgnoresRemoteEyeRedirection()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var user = Spawn(testMap, new Vector2(0.5f, 0.5f), 0);
            var physicalTarget = Spawn(testMap, new Vector2(0.5f, 0.5f), 0);
            var remoteEye = Spawn(testMap, new Vector2(0.5f, 0.5f), 1);
            var remoteTarget = Spawn(testMap, new Vector2(0.5f, 0.5f), 1);
            var eye = SEntMan.EnsureComponent<EyeComponent>(user);
            SEntMan.System<SharedEyeSystem>().SetTarget(user, remoteEye, eye);

            var authority = SEntMan.System<SharedZLevelInteractionSystem>();
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>();
            metrics.ResetCounters();
            Assert.Multiple(() =>
            {
                Assert.That(authority.AreOnSameWorldLevel(user, physicalTarget), Is.True);
                Assert.That(authority.AreOnSameWorldLevel(user, remoteTarget), Is.False);
                Assert.That(authority.CanDirectlyInteract(user, physicalTarget), Is.False);
                Assert.That(authority.CanDirectlyInteract(user, remoteTarget), Is.True);
            });

            var snapshot = metrics.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(snapshot.InteractionQueries, Is.EqualTo(2));
                Assert.That(snapshot.InteractionRemoteOriginQueries, Is.EqualTo(2));
                Assert.That(snapshot.InteractionSameLevelAllowed, Is.EqualTo(1));
                Assert.That(snapshot.InteractionDifferentLevelRejected, Is.EqualTo(1));
                Assert.That(snapshot.InteractionAllowed, Is.EqualTo(1));
                Assert.That(snapshot.InteractionRejected, Is.EqualTo(1));
                Assert.That(snapshot.InteractionPhysicalQueries, Is.EqualTo(2));
                Assert.That(snapshot.InteractionPhysicalAllowed, Is.EqualTo(1));
                Assert.That(snapshot.InteractionPhysicalRejected, Is.EqualTo(1));
            });
        });
    }

    [Test]
    public async Task InteractionMetricsClassifyInvalidMapAndFrameRejections()
    {
        var firstMap = await Pair.CreateTestMap();
        var secondMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(firstMap);
            Configure(secondMap);
            var origin = Spawn(firstMap, new Vector2(0.5f, 0.5f), 0);
            var differentMapTarget = Spawn(secondMap, new Vector2(0.5f, 0.5f), 0);

            var mapManager = Server.ResolveDependency<IMapManager>();
            var otherGrid = mapManager.CreateGridEntity(firstMap.MapId);
            var differentFrameTarget = SEntMan.SpawnEntity(
                null,
                new EntityCoordinates(otherGrid, new Vector2(0.5f, 0.5f)));
            Assert.That(
                SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(differentFrameTarget, 1),
                Is.True);

            var authority = SEntMan.System<SharedZLevelInteractionSystem>();
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>();
            metrics.ResetCounters();

            Assert.Multiple(() =>
            {
                Assert.That(authority.CanDirectlyInteract(EntityUid.Invalid, origin), Is.False);
                Assert.That(authority.CanDirectlyInteract(origin, differentMapTarget), Is.False);
                Assert.That(authority.CanInteractThroughOpenBoundary(origin, differentFrameTarget, 100f), Is.False);
            });

            var snapshot = metrics.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(snapshot.InteractionQueries, Is.EqualTo(3));
                Assert.That(snapshot.InteractionAllowed, Is.Zero);
                Assert.That(snapshot.InteractionRejected, Is.EqualTo(3));
                Assert.That(snapshot.InteractionInvalidContextRejected, Is.EqualTo(1));
                Assert.That(snapshot.InteractionDifferentMapRejected, Is.EqualTo(1));
                Assert.That(snapshot.InteractionFrameRejected, Is.EqualTo(1));
                Assert.That(snapshot.InteractionTraceRejected, Is.Zero);
                Assert.That(snapshot.TraceQueries, Is.Zero);
            });

            metrics.ResetCounters();
            var reset = metrics.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(reset.InteractionQueries, Is.Zero);
                Assert.That(reset.InteractionAllowed, Is.Zero);
                Assert.That(reset.InteractionRejected, Is.Zero);
                Assert.That(reset.InteractionInvalidContextRejected, Is.Zero);
                Assert.That(reset.InteractionDifferentMapRejected, Is.Zero);
                Assert.That(reset.InteractionFrameRejected, Is.Zero);
            });
        });
    }

    [TestCase("ZLevelStairsUp", 0, 1, true)]
    [TestCase("ZLevelStairsDown", 1, 0, true)]
    [TestCase("ZLevelLadder", 0, 1, true)]
    [TestCase("ZLevelFloorOpeningMarker", 1, 0, true)]
    [TestCase("ZLevelShaftMarker", 0, 1, true)]
    [TestCase("ZLevelGrateBoundaryMarker", 1, 0, false)]
    [TestCase("ZLevelSealedBoundaryMarker", 0, 1, false)]
    public async Task BoundaryPrototypesAuthorInteractionPolicy(
        string prototype,
        int providerLocalZ,
        int targetLocalZ,
        bool expected)
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var position = new Vector2(0.5f, 0.5f);
            var provider = SEntMan.SpawnEntity(prototype, new EntityCoordinates(testMap.Grid, position));
            Assert.That(
                SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(provider, providerLocalZ),
                Is.True);

            var user = Spawn(testMap, position, providerLocalZ);
            var target = Spawn(testMap, position, targetLocalZ);
            var authority = SEntMan.System<SharedZLevelInteractionSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(authority.CanDirectlyInteract(user, target), Is.False);
                Assert.That(authority.CanInteractThroughOpenBoundary(user, target, 1f), Is.EqualTo(expected));
            });
        });
    }

    [Test]
    public async Task WarmSameLevelAuthorityChecksDoNotAllocate()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var user = Spawn(testMap, new Vector2(0.5f, 0.5f), 0);
            var target = Spawn(testMap, new Vector2(0.5f, 0.5f), 0);
            var authority = SEntMan.System<SharedZLevelInteractionSystem>();
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>();

            for (var i = 0; i < 32; i++)
                Assert.That(authority.CanDirectlyInteract(user, target), Is.True);

            metrics.ResetCounters();
            var allowed = true;
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < AllocationIterations; i++)
                allowed &= authority.CanDirectlyInteract(user, target);
            var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            var snapshot = metrics.Snapshot();

            Assert.Multiple(() =>
            {
                Assert.That(allowed, Is.True);
                Assert.That(allocatedBytes, Is.Zero);
                Assert.That(snapshot.InteractionQueries, Is.EqualTo(AllocationIterations));
                Assert.That(snapshot.InteractionSameLevelAllowed, Is.EqualTo(AllocationIterations));
                Assert.That(snapshot.InteractionRejected, Is.Zero);
                Assert.That(snapshot.TraceQueries, Is.Zero);
            });
        });
    }

    [Test]
    public async Task PhysicalVerbsRevalidateAtExecutionWithoutBlockingInspectionOrAdminUse()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var user = Spawn(testMap, new Vector2(0.5f, 0.5f), 1);
            var target = Spawn(testMap, new Vector2(0.5f, 0.5f), 0);
            var sameFloorTarget = Spawn(testMap, new Vector2(0.5f, 0.5f), 1);
            var verbs = SEntMan.System<VerbSystem>();
            var executions = 0;
            Verb[] physicalVerbs =
            [
                new Verb(),
                new InteractionVerb(),
                new UtilityVerb(),
                new InnateVerb(),
                new AlternativeVerb(),
                new ActivationVerb(),
                new EquipmentVerb(),
            ];

            foreach (var verb in physicalVerbs)
            {
                verb.Act = () => executions++;
                verbs.ExecuteVerb(verb, user, target);
            }

            verbs.ExecuteVerb(
                new Verb
                {
                    Category = VerbCategory.Admin,
                    Act = () => executions++,
                },
                user,
                target);

            Assert.That(executions, Is.Zero,
                "Physical verbs and unauthenticated admin labels must reject a target on another world Z.");

            SetBoundary(
                testMap,
                Vector2i.Zero,
                0,
                opens: ZLevelBoundaryChannels.Interaction | ZLevelBoundaryChannels.Visibility);
            foreach (var verb in physicalVerbs)
                verbs.ExecuteVerb(verb, user, target);
            Assert.That(executions, Is.EqualTo(physicalVerbs.Length),
                "Every physical verb family may cross an authored open portal within normal use range.");

            foreach (var verb in physicalVerbs)
                verbs.ExecuteVerb(verb, user, sameFloorTarget);
            Assert.That(executions, Is.EqualTo(physicalVerbs.Length * 2),
                "All physical verb families must retain normal same-floor execution.");

            verbs.ExecuteVerb(new ExamineVerb { Act = () => executions++ }, user, target);
            verbs.ExecuteVerb(new VvVerb { Act = () => executions++ }, user, target);
            verbs.ExecuteVerb(new InteractionVerb { Act = () => executions++ }, user, target, forced: true);

            Assert.That(ServerSession, Is.Not.Null);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, user);
            var adminManager = Server.ResolveDependency<IAdminManager>();
            adminManager.PromoteHost(ServerSession!);
            verbs.ExecuteVerb(
                new Verb
                {
                    Category = VerbCategory.Admin,
                    Act = () => executions++,
                },
                user,
                target);
            adminManager.DeAdmin(ServerSession!);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, null);

            Assert.That(executions, Is.EqualTo(physicalVerbs.Length * 2 + 4),
                "Examine, VV, authenticated admin verbs, and explicit forced execution keep their remote semantics.");
        });
    }

    [Test]
    public async Task RejectedTargetActionsAreTerminalAndEntityTargetsRemainSameFloor()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var user = Spawn(testMap, new Vector2(0.5f, 0.5f), 0);
            var upperTarget = Spawn(testMap, new Vector2(0.5f, 0.5f), 1);
            var action = Spawn(testMap, new Vector2(0.5f, 0.5f), 0);
#pragma warning disable RA0002
            SEntMan.EnsureComponent<ActionComponent>(action).CheckCanInteract = false;
            var targetAction = SEntMan.EnsureComponent<TargetActionComponent>(action);
            targetAction.CheckCanAccess = false;
            targetAction.Range = 0f;
            SEntMan.EnsureComponent<EntityTargetActionComponent>(action).Event = new FunnelEntityActionEvent();
#pragma warning restore RA0002

            var actions = SEntMan.System<SharedActionsSystem>();
            Assert.That(
                actions.ValidateEntityTarget(
                    user,
                    upperTarget,
                    (action, SEntMan.GetComponent<EntityTargetActionComponent>(action))),
                Is.False,
                "Disabling planar access checks must not opt an entity action into another floor.");

            var validation = new ActionValidateEvent
            {
                Input = new RequestPerformActionEvent(
                    SEntMan.GetNetEntity(action),
                    SEntMan.GetNetEntity(upperTarget)),
                User = user,
                Provider = user,
            };
            SEntMan.EventBus.RaiseLocalEvent(action, ref validation);
            Assert.That(validation.Invalid, Is.True,
                "A rejected entity target must stop the server request before action execution.");

            var missingEntityValidation = new ActionValidateEvent
            {
                Input = new RequestPerformActionEvent(
                    SEntMan.GetNetEntity(action),
                    new NetEntity(int.MaxValue)),
                User = user,
                Provider = user,
            };
            SEntMan.EventBus.RaiseLocalEvent(action, ref missingEntityValidation);
            Assert.That(missingEntityValidation.Invalid, Is.True,
                "An unknown network entity must be rejected before position or rotation lookup.");

            var worldAction = Spawn(testMap, new Vector2(0.5f, 0.5f), 0);
#pragma warning disable RA0002
            SEntMan.EnsureComponent<ActionComponent>(worldAction).CheckCanInteract = false;
            var worldTarget = SEntMan.EnsureComponent<TargetActionComponent>(worldAction);
            worldTarget.CheckCanAccess = false;
            worldTarget.Range = 0.25f;
            SEntMan.EnsureComponent<WorldTargetActionComponent>(worldAction).Event = new FunnelWorldActionEvent();
#pragma warning restore RA0002
            var farCoordinates = new EntityCoordinates(testMap.Grid, new Vector2(20f, 20f));
            var worldValidation = new ActionValidateEvent
            {
                Input = new RequestPerformActionEvent(
                    SEntMan.GetNetEntity(worldAction),
                    SEntMan.GetNetCoordinates(farCoordinates)),
                User = user,
                Provider = user,
            };
            SEntMan.EventBus.RaiseLocalEvent(worldAction, ref worldValidation);
            Assert.That(worldValidation.Invalid, Is.True,
                "A rejected world target must also be terminal instead of executing with an unset event target.");

#pragma warning disable RA0002
            worldTarget.Range = 0f;
#pragma warning restore RA0002
            var nonFiniteCoordinates = new EntityCoordinates(
                testMap.Grid,
                new Vector2(float.NaN, 0f));
            var nonFiniteValidation = new ActionValidateEvent
            {
                Input = new RequestPerformActionEvent(
                    SEntMan.GetNetEntity(worldAction),
                    SEntMan.GetNetCoordinates(nonFiniteCoordinates)),
                User = user,
                Provider = user,
            };
            SEntMan.EventBus.RaiseLocalEvent(worldAction, ref nonFiniteValidation);
            Assert.That(nonFiniteValidation.Invalid, Is.True,
                "Unlimited-range world actions must still reject non-finite coordinates.");

            var nearCoordinates = new EntityCoordinates(testMap.Grid, new Vector2(0.5f, 0.5f));
            var wrongLayerValidation = new ActionValidateEvent
            {
                Input = new RequestPerformActionEvent(
                    SEntMan.GetNetEntity(worldAction),
                    SEntMan.GetNetCoordinates(nearCoordinates),
                    FrameOrigin + 1),
                User = user,
                Provider = user,
            };
            SEntMan.EventBus.RaiseLocalEvent(worldAction, ref wrongLayerValidation);
            Assert.That(wrongLayerValidation.Invalid, Is.True,
                "A coordinate-only action cannot smuggle a different world layer through a planar target.");

            var sameLayerValidation = new ActionValidateEvent
            {
                Input = new RequestPerformActionEvent(
                    SEntMan.GetNetEntity(worldAction),
                    SEntMan.GetNetCoordinates(nearCoordinates),
                    FrameOrigin),
                User = user,
                Provider = user,
            };
            SEntMan.EventBus.RaiseLocalEvent(worldAction, ref sameLayerValidation);
            var worldEvent = SEntMan.GetComponent<WorldTargetActionComponent>(worldAction).Event;
            Assert.Multiple(() =>
            {
                Assert.That(sameLayerValidation.Invalid, Is.False);
                Assert.That(worldEvent, Is.Not.Null);
                Assert.That(worldEvent!.TargetWorldZ, Is.EqualTo(FrameOrigin));
            });
        });
    }

    [Test]
    public async Task OptedInWorldActionsRequireAVisibleLowerFloorCoordinate()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var user = Spawn(testMap, new Vector2(0.5f, 0.5f), 1);
            var action = Spawn(testMap, new Vector2(0.5f, 0.5f), 1);
#pragma warning disable RA0002
            SEntMan.EnsureComponent<ActionComponent>(action).CheckCanInteract = false;
            var targetAction = SEntMan.EnsureComponent<TargetActionComponent>(action);
            targetAction.CheckCanAccess = false;
            targetAction.Range = 1.1f;
            var worldAction = SEntMan.EnsureComponent<WorldTargetActionComponent>(action);
            worldAction.AllowCrossLevelCoordinates = true;
            worldAction.Event = new FunnelWorldActionEvent();
#pragma warning restore RA0002

            var coordinates = new EntityCoordinates(testMap.Grid, new Vector2(0.5f, 0.5f));
            var visibility = SEntMan.System<SharedZLevelVisibilitySystem>();

            Assert.That(
                visibility.TryGetNearestVisibleLowerTileWorldZ(
                    coordinates,
                    testMap.MapId,
                    FrameOrigin + 1,
                    out _),
                Is.False,
                "A closed floor must not become an implicit lower coordinate target.");

            var closedValidation = new ActionValidateEvent
            {
                Input = new RequestPerformActionEvent(
                    SEntMan.GetNetEntity(action),
                    SEntMan.GetNetCoordinates(coordinates),
                    FrameOrigin),
                User = user,
                Provider = user,
            };
            SEntMan.EventBus.RaiseLocalEvent(action, ref closedValidation);
            Assert.That(closedValidation.Invalid, Is.True,
                "Opt-in alone must not bypass a closed visibility boundary.");

            SetBoundary(
                testMap,
                Vector2i.Zero,
                0,
                opens: ZLevelBoundaryChannels.Visibility);

            Assert.That(
                visibility.TryGetNearestVisibleLowerTileWorldZ(
                    coordinates,
                    testMap.MapId,
                    FrameOrigin + 1,
                    out var lowerWorldZ),
                Is.True);
            Assert.That(lowerWorldZ, Is.EqualTo(FrameOrigin));

            var openValidation = new ActionValidateEvent
            {
                Input = new RequestPerformActionEvent(
                    SEntMan.GetNetEntity(action),
                    SEntMan.GetNetCoordinates(coordinates),
                    lowerWorldZ),
                User = user,
                Provider = user,
            };
            SEntMan.EventBus.RaiseLocalEvent(action, ref openValidation);
            var worldEvent = SEntMan.GetComponent<WorldTargetActionComponent>(action).Event;
            Assert.Multiple(() =>
            {
                Assert.That(openValidation.Invalid, Is.False,
                    "An authored visible lower surface inside 3D range should be accepted.");
                Assert.That(worldEvent, Is.Not.Null);
                Assert.That(worldEvent!.TargetWorldZ, Is.EqualTo(FrameOrigin));
            });

            var aboveValidation = new ActionValidateEvent
            {
                Input = new RequestPerformActionEvent(
                    SEntMan.GetNetEntity(action),
                    SEntMan.GetNetCoordinates(coordinates),
                    FrameOrigin + 2),
                User = user,
                Provider = user,
            };
            SEntMan.EventBus.RaiseLocalEvent(action, ref aboveValidation);
            Assert.That(aboveValidation.Invalid, Is.True,
                "Coordinate-only action targeting remains downward-only.");

#pragma warning disable RA0002
            targetAction.Range = 0.9f;
#pragma warning restore RA0002
            var outOfRangeValidation = new ActionValidateEvent
            {
                Input = new RequestPerformActionEvent(
                    SEntMan.GetNetEntity(action),
                    SEntMan.GetNetCoordinates(coordinates),
                    FrameOrigin),
                User = user,
                Provider = user,
            };
            SEntMan.EventBus.RaiseLocalEvent(action, ref outOfRangeValidation);
            Assert.That(outOfRangeValidation.Invalid, Is.True,
                "The range check must include both planar and vertical distance.");

#pragma warning disable RA0002
            targetAction.Range = 0f;
#pragma warning restore RA0002
            var extremeLayerValidation = new ActionValidateEvent
            {
                Input = new RequestPerformActionEvent(
                    SEntMan.GetNetEntity(action),
                    SEntMan.GetNetCoordinates(coordinates),
                    int.MinValue),
                User = user,
                Provider = user,
            };
            Assert.DoesNotThrow(() =>
                SEntMan.EventBus.RaiseLocalEvent(action, ref extremeLayerValidation));
            Assert.That(extremeLayerValidation.Invalid, Is.True,
                "An extreme forged layer must be rejected before frame arithmetic.");

            var map = SEntMan.System<SharedMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
            map.SetZLevelTile(
                testMap.Grid,
                grid,
                new ZLevelTileIndices(0, 0, 0),
                Tile.Empty);
            Assert.That(
                visibility.TryGetNearestVisibleLowerTileWorldZ(
                    coordinates,
                    testMap.MapId,
                    FrameOrigin + 1,
                    out _),
                Is.False,
                "A sparse empty layer must not be exposed as an implicit target.");
        });
    }

    [Test]
    public async Task BuiAttemptCannotCrossFloors()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var user = Spawn(testMap, new Vector2(0.5f, 0.5f), 0);
            var target = Spawn(testMap, new Vector2(0.5f, 0.5f), 1);
            SEntMan.EnsureComponent<UserInterfaceComponent>(target);
            var attempt = new BoundUserInterfaceMessageAttempt(
                user,
                target,
                FunnelUiKey.Key,
                new OpenBoundInterfaceMessage());

            SEntMan.EventBus.RaiseLocalEvent(target, attempt);
            Assert.That(attempt.Cancelled, Is.True);
        });
    }

    [Test]
    public async Task DragDropRequestRevalidatesBothEntitiesOnServer()
    {
        var testMap = await Pair.CreateTestMap();
        NetEntity draggedNet = default;
        NetEntity targetNet = default;
        EntityUid dragged = default;
        EntityUid target = default;
        FunnelListenerSystem listener = default!;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            Assert.That(ServerSession, Is.Not.Null);
            var user = Spawn(testMap, new Vector2(0.5f, 0.5f), 0);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, user);

            dragged = Spawn(testMap, new Vector2(0.5f, 0.5f), 1);
            target = Spawn(testMap, new Vector2(0.5f, 0.5f), 1);
            SEntMan.AddComponent<FunnelListenerComponent>(dragged);
            SEntMan.AddComponent<FunnelListenerComponent>(target);
            draggedNet = SEntMan.GetNetEntity(dragged);
            targetNet = SEntMan.GetNetEntity(target);
            listener = SEntMan.System<FunnelListenerSystem>();
            listener.Reset();
        });
        await Pair.RunTicksSync(5);

        await Client.WaitPost(() =>
            CEntMan.RaisePredictiveEvent(new DragDropRequestEvent(draggedNet, targetNet)));
        await Pair.RunTicksSync(5);
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(listener.DraggedEvents, Is.Zero);
                Assert.That(listener.TargetEvents, Is.Zero);
            });

            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(dragged, 0), Is.True);
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(target, 0), Is.True);
        });

        await Client.WaitPost(() =>
            CEntMan.RaisePredictiveEvent(new DragDropRequestEvent(draggedNet, targetNet)));
        await Pair.RunTicksSync(5);
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(listener.DraggedEvents, Is.EqualTo(1));
                Assert.That(listener.TargetEvents, Is.EqualTo(1));
            });
            listener.Reset();
        });
    }

    [Test]
    public async Task PointerCoordinateLayerIsRevalidatedAfterNetworking()
    {
        var testMap = await Pair.CreateTestMap();
        NetEntity targetNet = default;
        EntityUid target = default;
        FunnelListenerSystem listener = default!;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            Assert.That(ServerSession, Is.Not.Null);
            var user = Spawn(testMap, new Vector2(0.5f, 0.5f), 0);
            SEntMan.AddComponent<ComplexInteractionComponent>(user);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, user);

            target = Spawn(testMap, new Vector2(0.5f, 0.5f), 0);
            SEntMan.AddComponent<FunnelListenerComponent>(target);
            targetNet = SEntMan.GetNetEntity(target);
            listener = SEntMan.System<FunnelListenerSystem>();
            listener.Reset();
        });
        await Pair.RunTicksSync(5);
        await AssertClientWorldZ(targetNet, FrameOrigin);

        // Leave the client on its last Z0 state while the authoritative target moves.
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(target, 1), Is.True));
        await SendPointerUse(targetNet, FrameOrigin, BoundKeyState.Down);
        await Pair.RunTicksSync(5);
        await AssertClientWorldZ(targetNet, FrameOrigin + 1);

        await Server.WaitAssertion(() =>
            Assert.That(listener.HandEvents, Is.Zero,
                "The server must reject a stale pointer layer even when the target identity is valid."));
        await SendPointerUse(targetNet, FrameOrigin + 1, BoundKeyState.Up);
        await Pair.RunTicksSync(1);
    }

    [Test]
    public async Task PointerCoordinateLayerPreservesSameFloorInteractionAfterNetworking()
    {
        var testMap = await Pair.CreateTestMap();
        NetEntity targetNet = default;
        FunnelListenerSystem listener = default!;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            Assert.That(ServerSession, Is.Not.Null);
            var user = Spawn(testMap, new Vector2(0.5f, 0.5f), 0);
            SEntMan.AddComponent<ComplexInteractionComponent>(user);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, user);

            var target = Spawn(testMap, new Vector2(0.5f, 0.5f), 0);
            SEntMan.AddComponent<FunnelListenerComponent>(target);
            targetNet = SEntMan.GetNetEntity(target);
            listener = SEntMan.System<FunnelListenerSystem>();
            listener.Reset();

            SEntMan.System<SharedInteractionSystem>().UserInteraction(
                user,
                SEntMan.GetComponent<TransformComponent>(target).Coordinates,
                target);
            Assert.That(listener.HandEvents, Is.EqualTo(1),
                "The fixture must permit the native same-floor interaction before testing transport.");
            listener.Reset();
        });
        await Pair.RunTicksSync(5);
        await AssertClientWorldZ(targetNet, FrameOrigin);

        await SendPointerUse(targetNet, FrameOrigin, BoundKeyState.Down);
        await Pair.RunTicksSync(5);
        await Server.WaitAssertion(() =>
            Assert.That(listener.HandEvents, Is.EqualTo(1),
                "A synchronized same-floor pointer layer must preserve native interaction."));
        await SendPointerUse(targetNet, FrameOrigin, BoundKeyState.Up);
        await Pair.RunTicksSync(1);
    }

    [Test]
    public async Task PointerUseRequiresAnAuthoredVerticalPortalAfterNetworking()
    {
        var testMap = await Pair.CreateTestMap();
        NetEntity targetNet = default;
        FunnelListenerSystem listener = default!;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            Assert.That(ServerSession, Is.Not.Null);
            var user = Spawn(testMap, new Vector2(0.5f, 0.5f), 1);
            SEntMan.AddComponent<ComplexInteractionComponent>(user);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, user);

            var target = Spawn(testMap, new Vector2(0.5f, 0.5f), 0);
            SEntMan.AddComponent<FunnelListenerComponent>(target);
            targetNet = SEntMan.GetNetEntity(target);
            listener = SEntMan.System<FunnelListenerSystem>();
            listener.Reset();
        });
        await Pair.RunTicksSync(5);
        await AssertClientWorldZ(targetNet, FrameOrigin);

        await SendPointerUse(targetNet, FrameOrigin, BoundKeyState.Down);
        await Pair.RunTicksSync(5);
        await SendPointerUse(targetNet, FrameOrigin, BoundKeyState.Up);
        await Pair.RunTicksSync(1);
        await Server.WaitAssertion(() =>
            Assert.That(listener.HandEvents, Is.Zero,
                "A correct coordinate layer must not bypass a closed Interaction boundary."));

        await Server.WaitAssertion(() =>
            SetBoundary(
                testMap,
                Vector2i.Zero,
                0,
                opens: ZLevelBoundaryChannels.Interaction | ZLevelBoundaryChannels.Visibility));
        await Pair.RunTicksSync(2);

        await SendPointerUse(targetNet, FrameOrigin, BoundKeyState.Down);
        await Pair.RunTicksSync(5);
        await Server.WaitAssertion(() =>
            Assert.That(listener.HandEvents, Is.EqualTo(1),
                "The same server-owned target becomes usable after an authored portal opens."));
        await SendPointerUse(targetNet, FrameOrigin, BoundKeyState.Up);
        await Pair.RunTicksSync(1);
    }

    private async Task SendPointerUse(NetEntity targetNet, int coordinateLayer, BoundKeyState state)
    {
        await Client.WaitPost(() =>
        {
            var inputManager = Client.ResolveDependency<IInputManager>();
            var input = CEntMan.System<Robust.Client.GameObjects.InputSystem>();
            var target = CEntMan.GetEntity(targetNet);
            var function = EngineKeyFunctions.Use;
            var functionId = inputManager.NetworkBindMap.KeyFunctionID(function);
            var message = new ClientFullInputCmdMessage(
                CGameTiming.CurTick,
                CGameTiming.TickFraction,
                functionId)
            {
                State = state,
                Coordinates = CEntMan.GetComponent<TransformComponent>(target).Coordinates,
                CoordinateLayer = coordinateLayer,
                Uid = target,
            };

            Assert.That(
                input.HandleInputCommand(Client.Session, function, message),
                Is.False,
                "A valid local pointer command must be dispatched to the server.");
        });
    }

    private async Task AssertClientWorldZ(NetEntity targetNet, int expectedWorldZ)
    {
        await Client.WaitAssertion(() =>
        {
            var target = CEntMan.GetEntity(targetNet);
            Assert.That(
                CEntMan.System<SharedZLevelSystem>().GetWorldZLevel(target),
                Is.EqualTo(expectedWorldZ));
        });
    }

    [Test]
    public async Task TargetedDoAfterRejectsAnotherFloorDuringInitialValidation()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var user = Spawn(testMap, new Vector2(0.5f, 0.5f), 0);
            var upperTarget = Spawn(testMap, new Vector2(0.5f, 0.5f), 1);
            var lowerTarget = Spawn(testMap, new Vector2(0.5f, 0.5f), 0);
            var component = SEntMan.EnsureComponent<DoAfterComponent>(user);
            var doAfter = SEntMan.System<SharedDoAfterSystem>();

            var rejected = new DoAfterArgs(
                SEntMan,
                user,
                TimeSpan.FromSeconds(10),
                new FunnelDoAfterEvent(),
                null,
                upperTarget)
            {
                Broadcast = true,
                DistanceThreshold = 1.5f,
            };
            Assert.That(doAfter.TryStartDoAfter(rejected, component), Is.False);

            var accepted = new DoAfterArgs(
                SEntMan,
                user,
                TimeSpan.FromSeconds(10),
                new FunnelDoAfterEvent(),
                null,
                lowerTarget)
            {
                Broadcast = true,
                DistanceThreshold = 1.5f,
            };
            Assert.That(doAfter.TryStartDoAfter(accepted, out var id, component), Is.True);
            doAfter.Cancel(id, component, force: true);
        });
    }

    [Test]
    public async Task InteractionRelayUsesItsServerOwnedEntityAsSpatialOrigin()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var user = Spawn(testMap, new Vector2(0.5f, 0.5f), 0);
            var relay = Spawn(testMap, new Vector2(0.5f, 0.5f), 1);
            var upperTarget = Spawn(testMap, new Vector2(0.5f, 0.5f), 1);
            var lowerTarget = Spawn(testMap, new Vector2(0.5f, 0.5f), 0);
            SEntMan.AddComponent<FunnelListenerComponent>(upperTarget);
            SEntMan.AddComponent<FunnelListenerComponent>(lowerTarget);
            SEntMan.AddComponent<InteractionRelayComponent>(user);
            SEntMan.AddComponent<ComplexInteractionComponent>(relay);
            var listener = SEntMan.System<FunnelListenerSystem>();
            listener.Reset();
            var interaction = SEntMan.System<SharedInteractionSystem>();
            interaction.SetRelay(user, relay);

            interaction.UserInteraction(
                user,
                SEntMan.GetComponent<TransformComponent>(upperTarget).Coordinates,
                upperTarget);
            interaction.UserInteraction(
                user,
                SEntMan.GetComponent<TransformComponent>(lowerTarget).Coordinates,
                lowerTarget);

            Assert.That(listener.HandEvents, Is.EqualTo(1),
                "The relay may act on its own floor, but must not fall back to the controller body's floor.");
            listener.Reset();
        });
    }

    [Test]
    public async Task StationAiEyePreservesWorldFloorAndCannotReopenBuiRangeAcrossFloors()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var core = SEntMan.SpawnEntity(
                "PlayerStationAiEmpty",
                new EntityCoordinates(testMap.Grid, new Vector2(0.5f, 0.5f)));
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(core, 1), Is.True);
            var brain = SEntMan.SpawnEntity(
                "StationAiBrain",
                new EntityCoordinates(testMap.Grid, new Vector2(0.5f, 0.5f)));
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(brain, 1), Is.True);
            var holder = SEntMan.GetComponent<StationAiHolderComponent>(core);
            Assert.That(
                SEntMan.System<ItemSlotsSystem>().TryInsert(core, holder.Slot, brain, null),
                Is.True);

            var stationAi = SEntMan.System<StationAiSystem>();
            var zLevels = SEntMan.System<SharedZLevelSystem>();
            var coreComponent = SEntMan.GetComponent<StationAiCoreComponent>(core);
            Assert.That(coreComponent.RemoteEntity, Is.Not.Null);
            Assert.That(zLevels.GetWorldZLevel(coreComponent.RemoteEntity!.Value), Is.EqualTo(FrameOrigin + 1));
            Assert.That(zLevels.SetZLevelPosition(coreComponent.RemoteEntity.Value, 2), Is.True);

            stationAi.SwitchRemoteEntityMode((core, coreComponent), false);
            Assert.That(coreComponent.RemoteEntity, Is.Not.Null);
            Assert.That(zLevels.GetWorldZLevel(coreComponent.RemoteEntity!.Value), Is.EqualTo(FrameOrigin + 2));
            stationAi.SwitchRemoteEntityMode((core, coreComponent), true);
            Assert.That(coreComponent.RemoteEntity, Is.Not.Null);
            Assert.That(zLevels.GetWorldZLevel(coreComponent.RemoteEntity!.Value), Is.EqualTo(FrameOrigin + 2));

            var bodyFloorTarget = Spawn(testMap, new Vector2(0.5f, 0.5f), 1);
            SEntMan.EnsureComponent<StationAiWhitelistComponent>(bodyFloorTarget);
            var range = new BoundUserInterfaceCheckRangeEvent(
                (bodyFloorTarget, SEntMan.GetComponent<TransformComponent>(bodyFloorTarget)),
                FunnelUiKey.Key,
                new InterfaceData("unused"),
                (brain, SEntMan.GetComponent<TransformComponent>(brain)))
            {
                Result = BoundUserInterfaceRangeResult.Pass,
            };
            SEntMan.EventBus.RaiseLocalEvent(bodyFloorTarget, ref range);
            Assert.That(range.Result, Is.EqualTo(BoundUserInterfaceRangeResult.Fail));
        });
    }

    private void Configure(TestMapData testMap)
    {
        SEntMan.System<SharedZLevelMapSystem>().Configure(
            testMap.MapUid,
            0,
            1,
            0,
            ZLevelDefaultBoundaryMode.TileAboveCloses);
        var transform = SEntMan.System<SharedTransformSystem>();
        Assert.That(transform.SetZLevelFrameOrigin(testMap.Grid, FrameOrigin), Is.True);
        transform.SetLocalPosition(testMap.Grid, new Vector2(8f, -5f));
        transform.SetLocalRotation(testMap.Grid, Angle.FromDegrees(27));

        var definitions = Server.ResolveDependency<ITileDefinitionManager>();
        var floor = (ContentTileDefinition) definitions["FloorSteel"];
        var map = SEntMan.System<SharedMapSystem>();
        var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
        for (var z = 0; z <= 1; z++)
        {
            for (var x = -1; x <= 1; x++)
            {
                for (var y = -1; y <= 1; y++)
                {
                    map.SetZLevelTile(testMap.Grid, grid, new ZLevelTileIndices(x, y, z), new Tile(floor.TileId));
                }
            }
        }
    }

    private EntityUid Spawn(TestMapData testMap, Vector2 position, int localZ)
    {
        return Spawn(testMap, null, position, localZ);
    }

    private EntityUid Spawn(TestMapData testMap, string prototype, Vector2 position, int localZ)
    {
        var entity = SEntMan.SpawnEntity(prototype, new EntityCoordinates(testMap.Grid, position));
        Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(entity, localZ), Is.True);
        return entity;
    }

    private EntityUid SetBoundary(
        TestMapData testMap,
        Vector2i tile,
        int lowerLocalZ,
        ZLevelBoundaryChannels opens,
        ZLevelBoundaryChannels closes = ZLevelBoundaryChannels.None)
    {
        var map = SEntMan.System<SharedMapSystem>();
        var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
        var provider = SEntMan.SpawnEntity(null, map.GridTileToLocal(testMap.Grid, grid, tile));
        Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(provider, lowerLocalZ), Is.True);
        var boundary = SEntMan.EnsureComponent<ZLevelBoundaryComponent>(provider);
        SEntMan.System<SharedZLevelBoundarySystem>().SetBoundary(
            (provider, boundary),
            true,
            1,
            opens,
            closes);
        SEntMan.System<SharedTransformSystem>().AnchorEntity(
            provider,
            SEntMan.GetComponent<TransformComponent>(provider));
        return provider;
    }

    private void SetBoundary(
        EntityUid provider,
        ZLevelBoundaryChannels opens,
        ZLevelBoundaryChannels closes)
    {
        var boundary = SEntMan.GetComponent<ZLevelBoundaryComponent>(provider);
        SEntMan.System<SharedZLevelBoundarySystem>().SetBoundary(
            (provider, boundary),
            true,
            1,
            opens,
            closes);
    }

    private sealed partial class FunnelEntityActionEvent : EntityTargetActionEvent;

    private sealed partial class FunnelWorldActionEvent : WorldTargetActionEvent;

    [Serializable, NetSerializable]
    private sealed partial class FunnelDoAfterEvent : SimpleDoAfterEvent;

    private enum FunnelUiKey : byte
    {
        Key,
    }

    [RegisterComponent]
    public sealed partial class FunnelListenerComponent : Component;

    public sealed class FunnelListenerSystem : EntitySystem
    {
        public int DraggedEvents { get; private set; }
        public int TargetEvents { get; private set; }
        public int HandEvents { get; private set; }

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<FunnelListenerComponent, DragDropDraggedEvent>(OnDragged);
            SubscribeLocalEvent<FunnelListenerComponent, DragDropTargetEvent>(OnTarget);
            SubscribeLocalEvent<FunnelListenerComponent, InteractHandEvent>(OnHand);
        }

        public void Reset()
        {
            DraggedEvents = 0;
            TargetEvents = 0;
            HandEvents = 0;
        }

        private void OnDragged(Entity<FunnelListenerComponent> ent, ref DragDropDraggedEvent args)
        {
            DraggedEvents++;
        }

        private void OnTarget(Entity<FunnelListenerComponent> ent, ref DragDropTargetEvent args)
        {
            TargetEvents++;
        }

        private void OnHand(Entity<FunnelListenerComponent> ent, ref InteractHandEvent args)
        {
            HandEvents++;
        }
    }
}
