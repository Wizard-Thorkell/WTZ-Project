// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Numerics;
using Content.Client.Mapping;
using Content.IntegrationTests.Fixtures;
using Content.Server.Administration.Managers;
using Content.Shared.ZLevel.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.ZLevel;

[TestFixture]
public sealed class ZLevelMappingSaveProtocolTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, DummyTicker = false, Dirty = true };

    [Test]
    public async Task ServerAlwaysCorrelatesAndClientRejectsConcurrentSaves()
    {
        var testMap = await Pair.CreateTestMap();
        var session = Pair.Player!;
        var player = session.AttachedEntity!.Value;
        var mapping = Client.ResolveDependency<MappingManager>();
        var admins = Server.ResolveDependency<IAdminManager>();

        await Server.WaitPost(() =>
        {
            SEntMan.System<SharedTransformSystem>().SetCoordinates(
                player,
                new EntityCoordinates(testMap.Grid, new Vector2(0.5f, 0.5f)));
            admins.DeAdmin(session);
        });
        await Pair.RunTicksSync(3);

        Task<MappingSaveResult> rejected = default!;
        await Client.WaitPost(() => rejected = mapping.SaveMap());
        await RunUntilCompleted(rejected!);
        Assert.That(await rejected!, Is.EqualTo(MappingSaveResult.ServerRejected),
            "An unauthorized request must receive an explicit correlated error instead of hanging.");

        await Server.WaitPost(() => admins.ReAdmin(session));
        await Pair.RunTicksSync(3);

        EntityUid invalidInfrastructure = default;
        await Server.WaitPost(() =>
        {
            invalidInfrastructure = SEntMan.SpawnEntity(
                "Wrench",
                new EntityCoordinates(testMap.Grid, new Vector2(0.5f, 0.5f)));
            SEntMan.EnsureComponent<ZLevelPositionComponent>(invalidInfrastructure).ZLevel = 1;
        });

        Task<MappingSaveResult> invalidMap = default!;
        await Client.WaitPost(() => invalidMap = mapping.SaveMap());
        await RunUntilCompleted(invalidMap);
        Assert.That(await invalidMap, Is.EqualTo(MappingSaveResult.ServerRejected),
            "Server-side snapshot validation must reject invalid authored Z state before opening a dialog.");
        await Server.WaitPost(() => SEntMan.DeleteEntity(invalidInfrastructure));

        Task<MappingSaveResult> first = default!;
        Task<MappingSaveResult> second = default!;
        await Client.WaitPost(() =>
        {
            first = mapping.SaveMap();
            second = mapping.SaveMap();
        });

        Assert.That(await second!, Is.EqualTo(MappingSaveResult.Busy));
        await RunUntilCompleted(first!);
        Assert.That(await first!, Is.EqualTo(MappingSaveResult.Cancelled),
            "A valid response must arrive before the headless file dialog reports cancellation.");
    }

    private async Task RunUntilCompleted(Task task)
    {
        for (var i = 0; i < 60 && !task.IsCompleted; i++)
        {
            await Pair.RunTicksSync(1);
        }

        Assert.That(task.IsCompleted, Is.True, "The mapping save protocol did not complete within 60 paired ticks.");
    }
}
