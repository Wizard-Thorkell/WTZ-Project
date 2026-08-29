using System.Threading.Tasks;
using Content.Client.Mapping;
using NUnit.Framework;

namespace Content.Tests.Client.Mapping;

[TestFixture]
public sealed class MappingSaveRequestTrackerTest
{
    [Test]
    public async Task KeepsRequestPendingUntilExplicitEnd()
    {
        var tracker = new MappingSaveRequestTracker();

        Assert.That(tracker.TryBegin(out var requestId, out var responseTask), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(requestId, Is.EqualTo(1));
            Assert.That(tracker.HasPending, Is.True);
            Assert.That(responseTask.IsCompleted, Is.False);
            Assert.That(tracker.TryBegin(out var rejectedId, out _), Is.False);
            Assert.That(rejectedId, Is.Zero);
        });

        Assert.That(tracker.TryCompleteData(requestId, "validated yaml"), Is.True);
        var response = await responseTask;
        Assert.Multiple(() =>
        {
            Assert.That(response.Yml, Is.EqualTo("validated yaml"));
            Assert.That(response.Error, Is.Null);
            Assert.That(tracker.HasPending, Is.True,
                "Receiving data must not allow another save while the dialog/write is still active.");
            Assert.That(tracker.TryCompleteData(requestId, "duplicate"), Is.False);
            Assert.That(tracker.TryBegin(out _, out _), Is.False);
        });

        Assert.That(tracker.TryEnd(requestId), Is.True);
        Assert.That(tracker.HasPending, Is.False);
        Assert.That(tracker.TryBegin(out var nextId, out _), Is.True);
        Assert.That(nextId, Is.EqualTo(2));
        Assert.That(tracker.TryEnd(nextId), Is.True);
    }

    [Test]
    public async Task IgnoresStaleAndMismatchedResponses()
    {
        var tracker = new MappingSaveRequestTracker();
        Assert.That(tracker.TryBegin(out var requestId, out var responseTask), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(tracker.TryCompleteData(requestId + 1, "stale"), Is.False);
            Assert.That(tracker.TryCompleteError(requestId + 1, "stale error"), Is.False);
            Assert.That(tracker.TryEnd(requestId + 1), Is.False);
            Assert.That(responseTask.IsCompleted, Is.False);
        });

        Assert.That(tracker.TryCompleteError(requestId, "server rejected"), Is.True);
        var response = await responseTask;
        Assert.Multiple(() =>
        {
            Assert.That(response.Yml, Is.Null);
            Assert.That(response.Error, Is.EqualTo("server rejected"));
        });
        Assert.That(tracker.TryEnd(requestId), Is.True);
    }

    [Test]
    public async Task TimeoutWinsAgainstLateDataAndReleasesForNextRequest()
    {
        var tracker = new MappingSaveRequestTracker();
        Assert.That(tracker.TryBegin(out var requestId, out var responseTask), Is.True);

        Assert.That(tracker.TryCompleteError(requestId, "timeout"), Is.True);
        Assert.That(tracker.TryCompleteData(requestId, "late yaml"), Is.False);
        Assert.That((await responseTask).Error, Is.EqualTo("timeout"));
        Assert.That(tracker.TryEnd(requestId), Is.True);

        Assert.That(tracker.TryBegin(out var nextId, out var nextTask), Is.True);
        Assert.That(nextId, Is.EqualTo(requestId + 1));
        Assert.That(tracker.TryCompleteData(nextId, "next yaml"), Is.True);
        Assert.That((await nextTask).Yml, Is.EqualTo("next yaml"));
        Assert.That(tracker.TryEnd(nextId), Is.True);
    }
}
