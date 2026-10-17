using Api.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests;

public sealed class ObjectDenialRecorderTests
{
    private static ObjectDenialEvent Event(string type = "building", string reason = ObjectAuthorizationService.NotFoundReason, DateTime? at = null)
        => new(at ?? DateTime.UtcNow, Guid.NewGuid(), null, type, Guid.NewGuid(), reason, ObjectDenialRecorder.IsProbeSignal(reason));

    [Fact]
    public void Recent_ReturnsNewestFirst_AndIsBoundedByCapacity()
    {
        var recorder = new ObjectDenialRecorder(capacity: 3);
        var start = DateTime.UtcNow;
        for (var i = 0; i < 5; i++)
        {
            recorder.Record(Event(at: start.AddSeconds(i)));
        }

        var recent = recorder.Recent(10);

        Assert.Equal(3, recent.Count);
        Assert.Equal(start.AddSeconds(4), recent[0].OccurredAtUtc);
        Assert.Equal(start.AddSeconds(2), recent[2].OccurredAtUtc);
        Assert.Equal(5, recorder.TotalRecorded); // the total keeps counting past the window
    }

    [Fact]
    public void Recent_HonoursTheRequestedLimit()
    {
        var recorder = new ObjectDenialRecorder();
        for (var i = 0; i < 5; i++)
        {
            recorder.Record(Event());
        }

        Assert.Equal(2, recorder.Recent(2).Count);
        Assert.Empty(recorder.Recent(0));
    }

    [Fact]
    public void Summarize_CountsByObjectTypeAndReason_AndSeparatesProbeSignals()
    {
        var recorder = new ObjectDenialRecorder();
        recorder.Record(Event("building", ObjectAuthorizationService.NotFoundReason));
        recorder.Record(Event("building", ObjectAuthorizationService.NotOwnedReason));
        recorder.Record(Event("company", ObjectAuthorizationService.NotOwnedReason));
        recorder.Record(Event("building", ObjectAuthorizationService.WrongContextReason));

        var summary = recorder.Summarize();

        Assert.Equal(4, summary.WindowSize);
        Assert.Equal(3, summary.ProbeSignals); // wrong_account_context is benign
        Assert.Equal(2, summary.ByReason.Single(r => r.Key == ObjectAuthorizationService.NotOwnedReason).Count);
        Assert.Equal(3, summary.ByObjectType.Single(r => r.Key == "building").Count);
    }

    [Theory]
    [InlineData(ObjectAuthorizationService.NotFoundReason, true)]
    [InlineData(ObjectAuthorizationService.NotOwnedReason, true)]
    [InlineData(ObjectAuthorizationService.WrongContextReason, false)]
    [InlineData(ObjectAuthorizationService.NotAvailableReason, false)]
    [InlineData("some_future_reason", true)] // fail safe: unknown reasons count as probe signals
    public void IsProbeSignal_MatchesTheServiceLogLevel(string reason, bool expected)
        => Assert.Equal(expected, ObjectDenialRecorder.IsProbeSignal(reason));

    [Fact]
    public void ObjectAuthorizationService_RecordsEveryDenialWithTheRealReason()
    {
        var recorder = new ObjectDenialRecorder();
        var service = new ObjectAuthorizationService(NullLogger<ObjectAuthorizationService>.Instance, new HttpContextAccessor(), recorder);
        var actor = Guid.NewGuid();
        var objectId = Guid.NewGuid();

        service.Deny(actor, "building", objectId, ObjectAuthorizationService.NotOwnedReason);
        service.LogDenied(actor, "company", Guid.NewGuid(), ObjectAuthorizationService.NotFoundReason);

        var recent = recorder.Recent(10);
        Assert.Equal(2, recent.Count);
        Assert.Equal(("company", ObjectAuthorizationService.NotFoundReason), (recent[0].ObjectType, recent[0].Reason));
        Assert.Equal((actor, objectId, "building", ObjectAuthorizationService.NotOwnedReason), (recent[1].ActorUserId, recent[1].ObjectId, recent[1].ObjectType, recent[1].Reason));
    }

    [Fact]
    public void ANoisyActor_CannotPushOtherActorsEventsOutOfTheWindow()
    {
        var recorder = new ObjectDenialRecorder(capacity: 10, perActorCapacity: 3);
        var quiet = Guid.NewGuid();
        var noisy = Guid.NewGuid();
        var at = DateTime.UtcNow;
        recorder.Record(new ObjectDenialEvent(at, quiet, null, "company", Guid.NewGuid(), ObjectAuthorizationService.NotOwnedReason, true));
        for (var i = 0; i < 50; i++)
        {
            recorder.Record(new ObjectDenialEvent(at.AddSeconds(i + 1), noisy, null, "building", Guid.NewGuid(), ObjectAuthorizationService.NotFoundReason, true));
        }

        var recent = recorder.Recent(100);

        Assert.Contains(recent, e => e.ActorUserId == quiet); // the quiet probe is still visible
        Assert.Equal(3, recent.Count(e => e.ActorUserId == noisy)); // the noisy actor keeps only its newest three
        Assert.Equal(51, recorder.TotalRecorded); // but every denial is still counted
        Assert.Equal(at.AddSeconds(50), recent.Where(e => e.ActorUserId == noisy).Max(e => e.OccurredAtUtc));
    }

    [Fact]
    public void Snapshot_ReturnsTheSummaryAndTheEventsFromTheSameView()
    {
        var recorder = new ObjectDenialRecorder();
        for (var i = 0; i < 4; i++)
        {
            recorder.Record(Event());
        }

        var (summary, recent) = recorder.Snapshot(2);

        Assert.Equal(4, summary.WindowSize);
        Assert.Equal(2, recent.Count);
    }
}
