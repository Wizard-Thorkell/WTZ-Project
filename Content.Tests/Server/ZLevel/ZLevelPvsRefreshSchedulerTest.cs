// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Collections.Generic;
using Content.Server.ZLevel.Systems;
using NUnit.Framework;

namespace Content.Tests.Server.ZLevel;

[TestFixture]
public sealed class ZLevelPvsRefreshSchedulerTest
{
    private const float FrameTime = 1f / 30f;

    [Test]
    public void StaggersThirtyTwoSessionsAcrossThreeFrames()
    {
        var scheduler = new ZLevelPvsRefreshScheduler(ZLevelPvsSystem.TargetRefreshInterval);

        var first = scheduler.Plan(32, FrameTime, 16);
        var second = scheduler.Plan(32, FrameTime, 16);
        var third = scheduler.Plan(32, FrameTime, 16);
        var fourth = scheduler.Plan(32, FrameTime, 16);

        Assert.Multiple(() =>
        {
            Assert.That(first.StartIndex, Is.Zero);
            Assert.That(first.ScheduledRefreshes, Is.EqualTo(10));
            Assert.That(first.DeferredRefreshes, Is.Zero);
            Assert.That(second.StartIndex, Is.EqualTo(10));
            Assert.That(second.ScheduledRefreshes, Is.EqualTo(11));
            Assert.That(second.DeferredRefreshes, Is.Zero);
            Assert.That(third.StartIndex, Is.EqualTo(21));
            Assert.That(third.ScheduledRefreshes, Is.EqualTo(11));
            Assert.That(third.DeferredRefreshes, Is.Zero);
            Assert.That(fourth.StartIndex, Is.Zero);
            Assert.That(fourth.ScheduledRefreshes, Is.EqualTo(10));
        });
    }

    [Test]
    public void BoundedBacklogRetainsCircularFairness()
    {
        var scheduler = new ZLevelPvsRefreshScheduler(ZLevelPvsSystem.TargetRefreshInterval);
        var refreshed = new HashSet<int>();

        for (var frame = 0; frame < 8; frame++)
        {
            var plan = scheduler.Plan(64, FrameTime, 8);
            Assert.Multiple(() =>
            {
                Assert.That(plan.StartIndex, Is.EqualTo(frame * 8));
                Assert.That(plan.ScheduledRefreshes, Is.EqualTo(8));
                Assert.That(plan.DeferredRefreshes, Is.GreaterThan(0));
            });

            for (var offset = 0; offset < plan.ScheduledRefreshes; offset++)
                refreshed.Add((plan.StartIndex + offset) % 64);
        }

        Assert.That(refreshed, Has.Count.EqualTo(64));
    }

    [Test]
    public void LongFrameIsCappedAndCatchupDoesNotDuplicateSessions()
    {
        var scheduler = new ZLevelPvsRefreshScheduler(ZLevelPvsSystem.TargetRefreshInterval);

        var first = scheduler.Plan(32, 10f, 16);
        var second = scheduler.Plan(32, 0f, 16);
        var third = scheduler.Plan(32, 0f, 16);

        Assert.Multiple(() =>
        {
            Assert.That(first.StartIndex, Is.Zero);
            Assert.That(first.DueRefreshes, Is.EqualTo(32));
            Assert.That(first.ScheduledRefreshes, Is.EqualTo(16));
            Assert.That(first.DeferredRefreshes, Is.EqualTo(16));
            Assert.That(second.StartIndex, Is.EqualTo(16));
            Assert.That(second.ScheduledRefreshes, Is.EqualTo(16));
            Assert.That(second.DeferredRefreshes, Is.Zero);
            Assert.That(third.ScheduledRefreshes, Is.Zero);
        });
    }

    [Test]
    public void EmptyPopulationResetsCreditAndCursor()
    {
        var scheduler = new ZLevelPvsRefreshScheduler(ZLevelPvsSystem.TargetRefreshInterval);
        scheduler.Plan(64, FrameTime, 8);

        var empty = scheduler.Plan(0, FrameTime, 8);
        var restarted = scheduler.Plan(3, ZLevelPvsSystem.TargetRefreshInterval, 3);

        Assert.Multiple(() =>
        {
            Assert.That(empty, Is.EqualTo(default(ZLevelPvsRefreshPlan)));
            Assert.That(restarted.StartIndex, Is.Zero);
            Assert.That(restarted.ScheduledRefreshes, Is.EqualTo(3));
            Assert.That(restarted.RemainingCredit, Is.EqualTo(0d).Within(1e-6));
        });
    }
}
