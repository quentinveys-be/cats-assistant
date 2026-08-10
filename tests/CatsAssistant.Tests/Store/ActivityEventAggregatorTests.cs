using CatsAssistant.Store;

namespace CatsAssistant.Tests.Store;

public class ActivityEventAggregatorTests
{
    [Fact]
    public void Aggregate_MergesConsecutiveDuplicateTitles_AndSplitsOnDifferentTitle()
    {
        var t0 = new DateTime(2026, 8, 10, 9, 0, 0, DateTimeKind.Utc);
        var t1 = t0.AddMinutes(5);
        var t2 = t0.AddMinutes(10);
        var events = new[]
        {
            new ActivityEvent(1, t0, ActivityEventKind.Foreground, "devenv.exe", "A", null),
            new ActivityEvent(2, t1, ActivityEventKind.TitleChange, "devenv.exe", "A", null),
            new ActivityEvent(3, t2, ActivityEventKind.Foreground, "chrome.exe", "B", null),
        };

        var segments = ActivityEventAggregator.Aggregate(events);

        Assert.Equal(2, segments.Count);
        Assert.Equal(new ActivitySegment(t0, t2, "devenv.exe", "A"), segments[0]);
        Assert.Equal(new ActivitySegment(t2, t2, "chrome.exe", "B"), segments[1]);
    }

    [Fact]
    public void Aggregate_SplitsSegmentAcrossIdlePeriod()
    {
        var t0 = new DateTime(2026, 8, 10, 9, 0, 0, DateTimeKind.Utc);
        var idleStart = t0.AddMinutes(5);
        var idleEnd = t0.AddMinutes(20);
        var t1 = t0.AddMinutes(21);
        var events = new[]
        {
            new ActivityEvent(1, t0, ActivityEventKind.Foreground, "devenv.exe", "A", null),
            new ActivityEvent(2, idleStart, ActivityEventKind.IdleStart, null, null, null),
            new ActivityEvent(3, idleEnd, ActivityEventKind.IdleEnd, null, null, null),
            new ActivityEvent(4, t1, ActivityEventKind.Foreground, "devenv.exe", "A", null),
        };

        var segments = ActivityEventAggregator.Aggregate(events);

        Assert.Equal(2, segments.Count);
        Assert.Equal(new ActivitySegment(t0, idleStart, "devenv.exe", "A"), segments[0]);
        Assert.Equal(new ActivitySegment(t1, t1, "devenv.exe", "A"), segments[1]);
    }

    [Fact]
    public void Aggregate_EmptySequence_ReturnsEmptyList()
    {
        var segments = ActivityEventAggregator.Aggregate(Array.Empty<ActivityEvent>());

        Assert.Empty(segments);
    }

    [Fact]
    public void Aggregate_SingleEvent_ReturnsSingleSegment()
    {
        var t0 = new DateTime(2026, 8, 10, 9, 0, 0, DateTimeKind.Utc);
        var events = new[]
        {
            new ActivityEvent(1, t0, ActivityEventKind.Foreground, "devenv.exe", "A", null),
        };

        var segments = ActivityEventAggregator.Aggregate(events);

        var segment = Assert.Single(segments);
        Assert.Equal(new ActivitySegment(t0, t0, "devenv.exe", "A"), segment);
    }
}
