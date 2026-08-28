#nullable enable annotations
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Interaction;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Item;
using Content.Shared.Verbs;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Reflection;

namespace Content.IntegrationTests.Tests.Interaction.Click
{
    [TestFixture]
    [TestOf(typeof(InteractionSystem))]
    public sealed partial class InteractionSystemTests : GameTest
    {
        [TestPrototypes]
        private const string Prototypes = @"
- type: entity
  id: DummyDebugWall
  components:
  - type: Physics
    bodyType: Dynamic
  - type: Fixtures
    fixtures:
      fix1:
        shape:
          !type:PhysShapeAabb
            bounds: ""-0.25,-0.25,0.25,0.25""
        layer:
        - MobMask
        mask:
        - MobMask
";

        [Test]
        public async Task InteractionTest()
        {
            var pair = Pair;
            var server = pair.Server;

            var sEntities = server.ResolveDependency<IEntityManager>();
            var mapManager = server.ResolveDependency<IMapManager>();
            var sysMan = server.ResolveDependency<IEntitySystemManager>();
            var handSys = sysMan.GetEntitySystem<SharedHandsSystem>();

            var map = await pair.CreateTestMap();
            var mapId = map.MapId;
            var coords = map.MapCoords;

            await server.WaitIdleAsync();
            EntityUid user = default;
            EntityUid target = default;
            EntityUid item = default;

            await server.WaitAssertion(() =>
            {
                user = sEntities.SpawnEntity(null, coords);
                sEntities.EnsureComponent<HandsComponent>(user);
                sEntities.EnsureComponent<ComplexInteractionComponent>(user);
                handSys.AddHand(user, "hand", HandLocation.Left);
                target = sEntities.SpawnEntity(null, coords);
                item = sEntities.SpawnEntity(null, coords);
                sEntities.EnsureComponent<ItemComponent>(item);
            });

            await server.WaitRunTicks(1);

            var entitySystemManager = server.ResolveDependency<IEntitySystemManager>();
            InteractionSystem interactionSystem = default!;
            TestInteractionSystem testInteractionSystem = default!;

            Assert.Multiple(() =>
            {
                Assert.That(entitySystemManager.TryGetEntitySystem(out interactionSystem));
                Assert.That(entitySystemManager.TryGetEntitySystem(out testInteractionSystem));
            });

            var interactUsing = false;
            var interactHand = false;
            await server.WaitAssertion(() =>
            {
                testInteractionSystem.InteractUsingEvent = (ev) => { Assert.That(ev.Target, Is.EqualTo(target)); interactUsing = true; };
                testInteractionSystem.InteractHandEvent = (ev) => { Assert.That(ev.Target, Is.EqualTo(target)); interactHand = true; };

                interactionSystem.UserInteraction(user, sEntities.GetComponent<TransformComponent>(target).Coordinates, target);
                Assert.Multiple(() =>
                {
                    Assert.That(interactUsing, Is.False);
                    Assert.That(interactHand);
                });

                Assert.That(handSys.TryPickup(user, item));

                interactionSystem.UserInteraction(user, sEntities.GetComponent<TransformComponent>(target).Coordinates, target);
                Assert.That(interactUsing);
            });

            testInteractionSystem.ClearHandlers();
        }

        [Test]
        public async Task InteractionRejectsTargetsOnAnotherWorldZLevel()
        {
            var pair = Pair;
            var server = pair.Server;
            var entities = server.ResolveDependency<IEntityManager>();
            var systems = server.ResolveDependency<IEntitySystemManager>();
            var map = await pair.CreateTestMap();

            EntityUid user = default;
            EntityUid target = default;
            await server.WaitAssertion(() =>
            {
                user = entities.SpawnEntity(null, map.MapCoords);
                entities.EnsureComponent<HandsComponent>(user);
                entities.EnsureComponent<ComplexInteractionComponent>(user);
                systems.GetEntitySystem<SharedHandsSystem>().AddHand(user, "hand", HandLocation.Left);
                target = entities.SpawnEntity(null, map.MapCoords);
                Assert.That(systems.GetEntitySystem<SharedZLevelSystem>().SetZLevelPosition(target, 1), Is.True);
            });

            var interaction = systems.GetEntitySystem<InteractionSystem>();
            var listener = systems.GetEntitySystem<TestInteractionSystem>();
            var interacted = false;

            await server.WaitAssertion(() =>
            {
                listener.InteractHandEvent = _ => interacted = true;
                interaction.UserInteraction(user, entities.GetComponent<TransformComponent>(target).Coordinates, target);
                Assert.That(interacted, Is.False);

                Assert.That(systems.GetEntitySystem<SharedZLevelSystem>().SetZLevelPosition(user, 1), Is.True);
                interaction.UserInteraction(user, entities.GetComponent<TransformComponent>(target).Coordinates, target);
                Assert.That(interacted, Is.True);
            });

            listener.ClearHandlers();
        }

        [Test]
        public async Task DirectInteractionEntryPointsRejectAnotherWorldZLevel()
        {
            var server = Pair.Server;
            var entities = server.ResolveDependency<IEntityManager>();
            var systems = server.ResolveDependency<IEntitySystemManager>();
            var map = await Pair.CreateTestMap();

            EntityUid user = default;
            EntityUid used = default;
            EntityUid target = default;
            await server.WaitAssertion(() =>
            {
                user = entities.SpawnEntity(null, map.MapCoords);
                entities.EnsureComponent<ComplexInteractionComponent>(user);
                used = entities.SpawnEntity(null, map.MapCoords);
                entities.EnsureComponent<TestDirectedInteractionComponent>(used);
                target = entities.SpawnEntity(null, map.MapCoords);
                Assert.That(systems.GetEntitySystem<SharedZLevelSystem>().SetZLevelPosition(target, 1), Is.True);
            });

            var interaction = systems.GetEntitySystem<InteractionSystem>();
            var listener = systems.GetEntitySystem<TestInteractionSystem>();
            var handInteractions = 0;
            var usingInteractions = 0;
            var activations = 0;
            var alternativeVerbs = 0;
            var beforeRangedInteractions = 0;
            var afterInteractions = 0;

            await server.WaitAssertion(() =>
            {
                listener.InteractHandEvent = ev =>
                {
                    handInteractions++;
                    ev.Handled = true;
                };
                listener.InteractUsingEvent = ev =>
                {
                    usingInteractions++;
                    ev.Handled = true;
                };
                listener.ActivateInWorldEvent = ev =>
                {
                    activations++;
                    ev.Handled = true;
                };
                listener.AlternativeVerbsEvent = ev => ev.Verbs.Add(new AlternativeVerb
                {
                    Text = "Z-level interaction test",
                    Act = () => alternativeVerbs++,
                });
                listener.BeforeRangedInteractEvent = ev =>
                {
                    beforeRangedInteractions++;
                    ev.Handled = true;
                };
                listener.AfterInteractEvent = ev =>
                {
                    afterInteractions++;
                    ev.Handled = true;
                };

                Assert.Multiple(() =>
                {
                    Assert.That(interaction.InRangeUnobstructed(user, target), Is.False);
                    interaction.InteractHand(user, target);
                    Assert.That(interaction.InteractUsing(
                        user,
                        used,
                        target,
                        entities.GetComponent<TransformComponent>(target).Coordinates,
                        checkCanInteract: false,
                        checkCanUse: false), Is.False);
                    Assert.That(interaction.InteractionActivate(
                        user,
                        target,
                        checkCanInteract: false,
                        checkUseDelay: false,
                        checkAccess: false), Is.False);
                    Assert.That(interaction.AltInteract(user, target), Is.False);
                    Assert.That(interaction.RangedInteractDoBefore(
                        user,
                        used,
                        target,
                        entities.GetComponent<TransformComponent>(target).Coordinates,
                        canReach: true), Is.False);
                    Assert.That(interaction.InteractDoAfter(
                        user,
                        used,
                        target,
                        entities.GetComponent<TransformComponent>(target).Coordinates,
                        canReach: true), Is.False);
                    Assert.That(handInteractions, Is.Zero);
                    Assert.That(usingInteractions, Is.Zero);
                    Assert.That(activations, Is.Zero);
                    Assert.That(alternativeVerbs, Is.Zero);
                    Assert.That(beforeRangedInteractions, Is.Zero);
                    Assert.That(afterInteractions, Is.Zero);
                });

                var zLevels = systems.GetEntitySystem<SharedZLevelSystem>();
                Assert.That(zLevels.SetZLevelPosition(user, 1), Is.True);
                Assert.That(zLevels.SetZLevelPosition(used, 1), Is.True);

                listener.BeforeRangedInteractEvent = null;
                listener.AfterInteractEvent = null;

                Assert.That(interaction.InRangeUnobstructed(user, target), Is.True);
                interaction.InteractHand(user, target);
                Assert.That(interaction.InteractUsing(
                    user,
                    used,
                    target,
                    entities.GetComponent<TransformComponent>(target).Coordinates,
                    checkCanInteract: false,
                    checkCanUse: false), Is.True);
                Assert.That(interaction.InteractionActivate(
                    user,
                    target,
                    checkCanInteract: false,
                    checkUseDelay: false,
                    checkAccess: false), Is.True);
                Assert.That(interaction.AltInteract(user, target), Is.True);

                listener.BeforeRangedInteractEvent = ev =>
                {
                    beforeRangedInteractions++;
                    ev.Handled = true;
                };
                listener.AfterInteractEvent = ev =>
                {
                    afterInteractions++;
                    ev.Handled = true;
                };
                Assert.That(interaction.RangedInteractDoBefore(
                    user,
                    used,
                    target,
                    entities.GetComponent<TransformComponent>(target).Coordinates,
                    canReach: true), Is.True);
                Assert.That(interaction.InteractDoAfter(
                    user,
                    used,
                    target,
                    entities.GetComponent<TransformComponent>(target).Coordinates,
                    canReach: true), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(handInteractions, Is.EqualTo(1));
                    Assert.That(usingInteractions, Is.EqualTo(1));
                    Assert.That(activations, Is.EqualTo(1));
                    Assert.That(alternativeVerbs, Is.EqualTo(1));
                    Assert.That(beforeRangedInteractions, Is.EqualTo(1));
                    Assert.That(afterInteractions, Is.EqualTo(1));
                });
            });

            listener.ClearHandlers();
        }

        [Test]
        public async Task RemoteEyeIsSpatialOriginWithoutBreakingLocalItems()
        {
            var server = Pair.Server;
            var entities = server.ResolveDependency<IEntityManager>();
            var systems = server.ResolveDependency<IEntitySystemManager>();
            var map = await Pair.CreateTestMap();

            EntityUid user = default;
            EntityUid remoteEye = default;
            EntityUid remoteTarget = default;
            EntityUid bodyFloorTarget = default;
            EntityUid localItem = default;
            await server.WaitAssertion(() =>
            {
                var zLevels = systems.GetEntitySystem<SharedZLevelSystem>();
                user = entities.SpawnEntity(null, map.MapCoords);
                entities.EnsureComponent<ComplexInteractionComponent>(user);
                var remoteCoordinates = map.MapCoords.Offset(new Vector2(10f, 0f));
                remoteEye = entities.SpawnEntity(null, remoteCoordinates);
                remoteTarget = entities.SpawnEntity(null, remoteCoordinates);
                bodyFloorTarget = entities.SpawnEntity(null, remoteCoordinates);
                localItem = entities.SpawnEntity(null, map.MapCoords);
                Assert.That(zLevels.SetZLevelPosition(remoteEye, 1), Is.True);
                Assert.That(zLevels.SetZLevelPosition(remoteTarget, 1), Is.True);

                var containerSystem = systems.GetEntitySystem<SharedContainerSystem>();
                var container = containerSystem.EnsureContainer<Container>(user, "zlevel-remote-local-item");
                Assert.That(containerSystem.Insert(localItem, container), Is.True);

                var eye = entities.EnsureComponent<EyeComponent>(user);
                systems.GetEntitySystem<SharedEyeSystem>().SetTarget(user, remoteEye, eye);
            });

            var interaction = systems.GetEntitySystem<InteractionSystem>();
            var zLevelInteraction = systems.GetEntitySystem<SharedZLevelInteractionSystem>();
            var listener = systems.GetEntitySystem<TestInteractionSystem>();
            var handInteractions = 0;
            var activations = 0;

            await server.WaitAssertion(() =>
            {
                listener.InteractHandEvent = ev =>
                {
                    handInteractions++;
                    ev.Handled = true;
                };
                listener.ActivateInWorldEvent = ev =>
                {
                    activations++;
                    ev.Handled = true;
                };

                Assert.That(zLevelInteraction.TryGetSpatialOrigin(user, remoteTarget, out var origin), Is.True);
                Assert.That(origin, Is.EqualTo(remoteEye));
                Assert.That(interaction.InRangeUnobstructed(user, remoteTarget), Is.True);
                interaction.UserInteraction(
                    user,
                    entities.GetComponent<TransformComponent>(remoteTarget).Coordinates,
                    remoteTarget);
                interaction.UserInteraction(
                    user,
                    entities.GetComponent<TransformComponent>(bodyFloorTarget).Coordinates,
                    bodyFloorTarget);

                Assert.That(interaction.InteractionActivate(
                    user,
                    localItem,
                    checkCanInteract: false,
                    checkUseDelay: false,
                    checkAccess: false), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(handInteractions, Is.EqualTo(1));
                    Assert.That(activations, Is.EqualTo(1));
                    Assert.That(zLevelInteraction.TryGetSpatialOrigin(user, localItem, out var localOrigin), Is.True);
                    Assert.That(localOrigin, Is.EqualTo(user));
                });
            });

            listener.ClearHandlers();
        }

        [Test]
        public async Task InteractionObstructionTest()
        {
            var pair = Pair;
            var server = pair.Server;

            var sEntities = server.ResolveDependency<IEntityManager>();
            var mapManager = server.ResolveDependency<IMapManager>();
            var sysMan = server.ResolveDependency<IEntitySystemManager>();
            var handSys = sysMan.GetEntitySystem<SharedHandsSystem>();

            var map = await pair.CreateTestMap();
            var mapId = map.MapId;
            var coords = map.MapCoords;

            await server.WaitIdleAsync();
            EntityUid user = default;
            EntityUid target = default;
            EntityUid item = default;
            EntityUid wall = default;

            await server.WaitAssertion(() =>
            {
                user = sEntities.SpawnEntity(null, coords);
                sEntities.EnsureComponent<HandsComponent>(user);
                handSys.AddHand(user, "hand", HandLocation.Left);
                target = sEntities.SpawnEntity(null, new MapCoordinates(new Vector2(1.9f, 0), mapId));
                item = sEntities.SpawnEntity(null, coords);
                sEntities.EnsureComponent<ItemComponent>(item);
                wall = sEntities.SpawnEntity("DummyDebugWall", new MapCoordinates(new Vector2(1, 0), sEntities.GetComponent<TransformComponent>(user).MapID));
            });

            await server.WaitRunTicks(1);

            var entitySystemManager = server.ResolveDependency<IEntitySystemManager>();
            InteractionSystem interactionSystem = default!;
            TestInteractionSystem testInteractionSystem = default!;
            Assert.Multiple(() =>
            {
                Assert.That(entitySystemManager.TryGetEntitySystem(out interactionSystem));
                Assert.That(entitySystemManager.TryGetEntitySystem(out testInteractionSystem));
            });

            var interactUsing = false;
            var interactHand = false;
            await server.WaitAssertion(() =>
            {
                testInteractionSystem.InteractUsingEvent = (ev) => { Assert.That(ev.Target, Is.EqualTo(target)); interactUsing = true; };
                testInteractionSystem.InteractHandEvent = (ev) => { Assert.That(ev.Target, Is.EqualTo(target)); interactHand = true; };

                interactionSystem.UserInteraction(user, sEntities.GetComponent<TransformComponent>(target).Coordinates, target);
                Assert.Multiple(() =>
                {
                    Assert.That(interactUsing, Is.False);
                    Assert.That(interactHand, Is.False);
                });

                Assert.That(handSys.TryPickup(user, item));

                interactionSystem.UserInteraction(user, sEntities.GetComponent<TransformComponent>(target).Coordinates, target);
                Assert.That(interactUsing, Is.False);
            });

            testInteractionSystem.ClearHandlers();
        }

        [Test]
        public async Task InteractionInRangeTest()
        {
            var pair = Pair;
            var server = pair.Server;

            var sEntities = server.ResolveDependency<IEntityManager>();
            var mapManager = server.ResolveDependency<IMapManager>();
            var sysMan = server.ResolveDependency<IEntitySystemManager>();
            var handSys = sysMan.GetEntitySystem<SharedHandsSystem>();

            var map = await pair.CreateTestMap();
            var mapId = map.MapId;
            var coords = map.MapCoords;

            await server.WaitIdleAsync();
            EntityUid user = default;
            EntityUid target = default;
            EntityUid item = default;

            await server.WaitAssertion(() =>
            {
                user = sEntities.SpawnEntity(null, coords);
                sEntities.EnsureComponent<HandsComponent>(user);
                sEntities.EnsureComponent<ComplexInteractionComponent>(user);
                handSys.AddHand(user, "hand", HandLocation.Left);
                target = sEntities.SpawnEntity(null, new MapCoordinates(new Vector2(SharedInteractionSystem.InteractionRange - 0.1f, 0), mapId));
                item = sEntities.SpawnEntity(null, coords);
                sEntities.EnsureComponent<ItemComponent>(item);
            });

            await server.WaitRunTicks(1);

            var entitySystemManager = server.ResolveDependency<IEntitySystemManager>();
            InteractionSystem interactionSystem = default!;
            TestInteractionSystem testInteractionSystem = default!;
            Assert.Multiple(() =>
            {
                Assert.That(entitySystemManager.TryGetEntitySystem(out interactionSystem));
                Assert.That(entitySystemManager.TryGetEntitySystem(out testInteractionSystem));
            });

            var interactUsing = false;
            var interactHand = false;
            await server.WaitAssertion(() =>
            {
                testInteractionSystem.InteractUsingEvent = (ev) => { Assert.That(ev.Target, Is.EqualTo(target)); interactUsing = true; };
                testInteractionSystem.InteractHandEvent = (ev) => { Assert.That(ev.Target, Is.EqualTo(target)); interactHand = true; };

                interactionSystem.UserInteraction(user, sEntities.GetComponent<TransformComponent>(target).Coordinates, target);
                Assert.Multiple(() =>
                {
                    Assert.That(interactUsing, Is.False);
                    Assert.That(interactHand);
                });

                Assert.That(handSys.TryPickup(user, item));

                interactionSystem.UserInteraction(user, sEntities.GetComponent<TransformComponent>(target).Coordinates, target);
                Assert.That(interactUsing);
            });

            testInteractionSystem.ClearHandlers();
        }


        [Test]
        public async Task InteractionOutOfRangeTest()
        {
            var pair = Pair;
            var server = pair.Server;

            var sEntities = server.ResolveDependency<IEntityManager>();
            var mapManager = server.ResolveDependency<IMapManager>();
            var sysMan = server.ResolveDependency<IEntitySystemManager>();
            var handSys = sysMan.GetEntitySystem<SharedHandsSystem>();

            var map = await pair.CreateTestMap();
            var mapId = map.MapId;
            var coords = map.MapCoords;

            await server.WaitIdleAsync();
            EntityUid user = default;
            EntityUid target = default;
            EntityUid item = default;

            await server.WaitAssertion(() =>
            {
                user = sEntities.SpawnEntity(null, coords);
                sEntities.EnsureComponent<HandsComponent>(user);
                handSys.AddHand(user, "hand", HandLocation.Left);
                target = sEntities.SpawnEntity(null, new MapCoordinates(new Vector2(SharedInteractionSystem.InteractionRange + 0.01f, 0), mapId));
                item = sEntities.SpawnEntity(null, coords);
                sEntities.EnsureComponent<ItemComponent>(item);
            });

            await server.WaitRunTicks(1);

            var entitySystemManager = server.ResolveDependency<IEntitySystemManager>();
            InteractionSystem interactionSystem = default!;
            TestInteractionSystem testInteractionSystem = default!;
            Assert.Multiple(() =>
            {
                Assert.That(entitySystemManager.TryGetEntitySystem(out interactionSystem));
                Assert.That(entitySystemManager.TryGetEntitySystem(out testInteractionSystem));
            });

            var interactUsing = false;
            var interactHand = false;
            await server.WaitAssertion(() =>
            {
                testInteractionSystem.InteractUsingEvent = (ev) => { Assert.That(ev.Target, Is.EqualTo(target)); interactUsing = true; };
                testInteractionSystem.InteractHandEvent = (ev) => { Assert.That(ev.Target, Is.EqualTo(target)); interactHand = true; };

                interactionSystem.UserInteraction(user, sEntities.GetComponent<TransformComponent>(target).Coordinates, target);
                Assert.Multiple(() =>
                {
                    Assert.That(interactUsing, Is.False);
                    Assert.That(interactHand, Is.False);
                });

                Assert.That(handSys.TryPickup(user, item));

                interactionSystem.UserInteraction(user, sEntities.GetComponent<TransformComponent>(target).Coordinates, target);
                Assert.That(interactUsing, Is.False);
            });

            testInteractionSystem.ClearHandlers();
        }

        [Test]
        public async Task InsideContainerInteractionBlockTest()
        {
            var pair = Pair;
            var server = pair.Server;

            var sEntities = server.ResolveDependency<IEntityManager>();
            var mapManager = server.ResolveDependency<IMapManager>();
            var sysMan = server.ResolveDependency<IEntitySystemManager>();
            var handSys = sysMan.GetEntitySystem<SharedHandsSystem>();
            var conSystem = sysMan.GetEntitySystem<SharedContainerSystem>();

            var map = await pair.CreateTestMap();
            var mapId = map.MapId;
            var coords = map.MapCoords;

            await server.WaitIdleAsync();
            EntityUid user = default;
            EntityUid target = default;
            EntityUid item = default;
            EntityUid containerEntity = default;
            BaseContainer container = null;

            await server.WaitAssertion(() =>
            {
                user = sEntities.SpawnEntity(null, coords);
                sEntities.EnsureComponent<HandsComponent>(user);
                sEntities.EnsureComponent<ComplexInteractionComponent>(user);
                handSys.AddHand(user, "hand", HandLocation.Left);
                target = sEntities.SpawnEntity(null, coords);
                item = sEntities.SpawnEntity(null, coords);
                sEntities.EnsureComponent<ItemComponent>(item);
                containerEntity = sEntities.SpawnEntity(null, coords);
                container = conSystem.EnsureContainer<Container>(containerEntity, "InteractionTestContainer");
            });

            await server.WaitRunTicks(1);

            var entitySystemManager = server.ResolveDependency<IEntitySystemManager>();
            InteractionSystem interactionSystem = default!;
            TestInteractionSystem testInteractionSystem = default!;
            Assert.Multiple(() =>
            {
                Assert.That(entitySystemManager.TryGetEntitySystem(out interactionSystem));
                Assert.That(entitySystemManager.TryGetEntitySystem(out testInteractionSystem));
            });

            await server.WaitIdleAsync();

            var interactUsing = false;
            var interactHand = false;
            await server.WaitAssertion(() =>
            {
#pragma warning disable NUnit2045 // Interdependent assertions.
                Assert.That(conSystem.Insert(user, container));
                Assert.That(sEntities.GetComponent<TransformComponent>(user).ParentUid, Is.EqualTo(containerEntity));
#pragma warning restore NUnit2045

                testInteractionSystem.InteractUsingEvent = (ev) => { Assert.That(ev.Target, Is.EqualTo(containerEntity)); interactUsing = true; };
                testInteractionSystem.InteractHandEvent = (ev) => { Assert.That(ev.Target, Is.EqualTo(containerEntity)); interactHand = true; };

                interactionSystem.UserInteraction(user, sEntities.GetComponent<TransformComponent>(target).Coordinates, target);
                Assert.Multiple(() =>
                {
                    Assert.That(interactUsing, Is.False);
                    Assert.That(interactHand, Is.False);
                });

                interactionSystem.UserInteraction(user, sEntities.GetComponent<TransformComponent>(containerEntity).Coordinates, containerEntity);
                Assert.Multiple(() =>
                {
                    Assert.That(interactUsing, Is.False);
                    Assert.That(interactHand);
                });

                Assert.That(handSys.TryPickup(user, item));

                interactionSystem.UserInteraction(user, sEntities.GetComponent<TransformComponent>(target).Coordinates, target);
                Assert.That(interactUsing, Is.False);

                interactionSystem.UserInteraction(user, sEntities.GetComponent<TransformComponent>(containerEntity).Coordinates, containerEntity);
                Assert.That(interactUsing, Is.True);
            });

            testInteractionSystem.ClearHandlers();
        }

        public sealed class TestInteractionSystem : EntitySystem
        {
            public EntityEventHandler<InteractUsingEvent>? InteractUsingEvent;
            public EntityEventHandler<InteractHandEvent>? InteractHandEvent;
            public EntityEventHandler<ActivateInWorldEvent>? ActivateInWorldEvent;
            public EntityEventHandler<GetVerbsEvent<AlternativeVerb>>? AlternativeVerbsEvent;
            public EntityEventHandler<BeforeRangedInteractEvent>? BeforeRangedInteractEvent;
            public EntityEventHandler<AfterInteractEvent>? AfterInteractEvent;

            public override void Initialize()
            {
                base.Initialize();
                SubscribeLocalEvent<InteractUsingEvent>((e) => InteractUsingEvent?.Invoke(e));
                SubscribeLocalEvent<InteractHandEvent>((e) => InteractHandEvent?.Invoke(e));
                SubscribeLocalEvent<ActivateInWorldEvent>((e) => ActivateInWorldEvent?.Invoke(e));
                SubscribeLocalEvent<GetVerbsEvent<AlternativeVerb>>((e) => AlternativeVerbsEvent?.Invoke(e));
                SubscribeLocalEvent<TestDirectedInteractionComponent, BeforeRangedInteractEvent>(OnBeforeRangedInteract);
                SubscribeLocalEvent<TestDirectedInteractionComponent, AfterInteractEvent>(OnAfterInteract);
            }

            private void OnBeforeRangedInteract(
                Entity<TestDirectedInteractionComponent> ent,
                ref BeforeRangedInteractEvent ev)
            {
                BeforeRangedInteractEvent?.Invoke(ev);
            }

            private void OnAfterInteract(
                Entity<TestDirectedInteractionComponent> ent,
                ref AfterInteractEvent ev)
            {
                AfterInteractEvent?.Invoke(ev);
            }

            public void ClearHandlers()
            {
                InteractUsingEvent = null;
                InteractHandEvent = null;
                ActivateInWorldEvent = null;
                AlternativeVerbsEvent = null;
                BeforeRangedInteractEvent = null;
                AfterInteractEvent = null;
            }
        }

        [RegisterComponent]
        public sealed partial class TestDirectedInteractionComponent : Component;

    }
}
