using System.Collections.Generic;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Interaction;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Puller;

#nullable enable

public sealed class InteractingEntitiesTest : InteractionTest
{
    private static readonly EntProtoId MobHuman = "MobHuman";

    /// <summary>
    /// Spawns a Target mob, and a second mob which drags it,
    /// and checks that the dragger is considered to be interacting with the dragged mob.
    /// </summary>
    [Test]
    public async Task PullerIsConsideredInteractingTest()
    {
        await SpawnTarget(MobHuman);
        var puller = await SpawnEntity(MobHuman, ToServer(TargetCoords));

        var pullSys = SEntMan.System<PullingSystem>();
        await Server.WaitAssertion(() =>
        {
            Assert.That(pullSys.TryStartPull(puller, ToServer(Target.Value)),
                $"{puller} failed to start pulling {Target}");
        });

        var list = new HashSet<EntityUid>();
        Server.System<SharedInteractionSystem>()
            .GetEntitiesInteractingWithTarget(ToServer(Target.Value), list);
        Assert.That(list, Is.EquivalentTo([puller]), $"{puller} was not considered to be interacting with {Target}");
    }

    [Test]
    public async Task CannotStartPullAcrossWorldZLevels()
    {
        await SpawnTarget(MobHuman);
        var target = ToServer(Target.Value);
        var puller = await SpawnEntity(MobHuman, ToServer(TargetCoords));
        var pullSys = SEntMan.System<PullingSystem>();
        var zLevel = SEntMan.System<SharedZLevelSystem>();

        await Server.WaitAssertion(() =>
        {
            Assert.That(zLevel.SetZLevelPosition(target, 1), Is.True);
            Assert.That(pullSys.TryStartPull(puller, target), Is.False);

            Assert.That(zLevel.SetZLevelPosition(puller, 1), Is.True);
            Assert.That(pullSys.TryStartPull(puller, target), Is.True);
        });
    }
}
