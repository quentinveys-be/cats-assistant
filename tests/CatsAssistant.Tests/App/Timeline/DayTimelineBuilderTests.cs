using CatsAssistant.App.Timeline;
using CatsAssistant.Connectors;
using CatsAssistant.Correlator;
using CatsAssistant.Store;

namespace CatsAssistant.Tests.App.Timeline;

public class DayTimelineBuilderTests
{
    private static readonly DateTime Day = new(2026, 8, 11, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Build_NoActivityNoIdleNoMeetings_ReturnsEmptyTimeline()
    {
        var result = DayTimelineBuilder.Build(
            Array.Empty<ActivityEvent>(),
            new CorrelationResult([], [], []),
            Array.Empty<CalendarEventData>(),
            Array.Empty<TimeBlockRow>());

        Assert.True(result.IsEmpty);
        Assert.Empty(result.Hours);
        Assert.Empty(result.Segments);
    }

    [Fact]
    public void Build_SingleCorrelatedBlock_ProducesOneSegmentAndOneGroupNoGap()
    {
        var events = new[]
        {
            new ActivityEvent(1, Day, ActivityEventKind.Foreground, "idea64.exe", "ULISTROIS-3101", null),
            new ActivityEvent(2, Day.AddMinutes(20), ActivityEventKind.IdleStart, null, null, null),
        };
        var block = new CorrelatedBlock(Day, Day.AddMinutes(20), "ULISTROIS-3101", null);
        var correlation = new CorrelationResult([block], [], []);

        var result = DayTimelineBuilder.Build(events, correlation, [], []);

        var segment = Assert.Single(result.Segments);
        Assert.Equal(TimelineHue.Hue1, segment.Hue);

        var group = Assert.Single(result.Groups);
        Assert.Equal("ULISTROIS-3101", group.JiraKey);
        Assert.Equal(TimelineHue.Hue1, group.Hue);
        Assert.Equal(1, group.PlageIndex);
        Assert.Equal(1, group.PlageCount);
        Assert.Equal(TimeBlockStatus.Proposed, group.Status);

        Assert.Empty(result.Gaps);
    }

    [Fact]
    public void Build_UncorrelatedBlock_ProducesGapAndUncorrelatedSegment()
    {
        var events = new[]
        {
            new ActivityEvent(1, Day, ActivityEventKind.Foreground, "chrome.exe", "sans ticket", null),
            new ActivityEvent(2, Day.AddMinutes(20), ActivityEventKind.IdleStart, null, null, null),
        };
        var block = new CorrelatedBlock(Day, Day.AddMinutes(20), null, null);
        var correlation = new CorrelationResult([block], [], []);

        var result = DayTimelineBuilder.Build(events, correlation, [], []);

        Assert.Equal(TimelineHue.Uncorrelated, Assert.Single(result.Segments).Hue);
        var gap = Assert.Single(result.Gaps);
        Assert.Equal("à imputer · 0:20", gap.Label);
        Assert.Empty(result.Groups);
    }

    [Fact]
    public void Build_NoAttributionBlock_ProducesNoGap()
    {
        var events = new[]
        {
            new ActivityEvent(1, Day, ActivityEventKind.Foreground, "OUTLOOK.EXE", "Réunion", null),
            new ActivityEvent(2, Day.AddMinutes(20), ActivityEventKind.IdleStart, null, null, null),
        };
        var block = new CorrelatedBlock(Day, Day.AddMinutes(20), null, null, NoAttribution: true);
        var correlation = new CorrelationResult([block], [], []);

        var result = DayTimelineBuilder.Build(events, correlation, [], []);

        Assert.Empty(result.Gaps);
        Assert.Empty(result.Groups);
    }

    [Fact]
    public void Build_IdlePeriod_ProducesIdleSegmentExcludedFromGaps()
    {
        var idle = new IdlePeriod(Day, Day.AddMinutes(20));
        var correlation = new CorrelationResult([], [idle], []);

        var result = DayTimelineBuilder.Build(Array.Empty<ActivityEvent>(), correlation, [], []);

        var segment = Assert.Single(result.Segments);
        Assert.Equal(TimelineHue.Idle, segment.Hue);
        Assert.Empty(result.Gaps);
    }

    [Fact]
    public void Build_FiveDistinctTickets_HueCyclesAfterFour()
    {
        var blocks = new[]
        {
            new CorrelatedBlock(Day, Day.AddMinutes(15), "T-1", null),
            new CorrelatedBlock(Day.AddMinutes(15), Day.AddMinutes(30), "T-2", null),
            new CorrelatedBlock(Day.AddMinutes(30), Day.AddMinutes(45), "T-3", null),
            new CorrelatedBlock(Day.AddMinutes(45), Day.AddMinutes(60), "T-4", null),
            new CorrelatedBlock(Day.AddMinutes(60), Day.AddMinutes(75), "T-5", null),
        };
        var correlation = new CorrelationResult(blocks, [], []);
        var events = SpanningEvents(Day, Day.AddMinutes(75));

        var result = DayTimelineBuilder.Build(events, correlation, [], []);

        Assert.Equal(TimelineHue.Hue1, result.Groups[0].Hue);
        Assert.Equal(TimelineHue.Hue2, result.Groups[1].Hue);
        Assert.Equal(TimelineHue.Hue3, result.Groups[2].Hue);
        Assert.Equal(TimelineHue.Hue4, result.Groups[3].Hue);
        Assert.Equal(TimelineHue.Hue1, result.Groups[4].Hue);
    }

    [Fact]
    public void Build_SameTicketTwoRuns_NumbersPlagesInChronologicalOrder()
    {
        var blocks = new[]
        {
            new CorrelatedBlock(Day, Day.AddMinutes(20), "ULISTROIS-3428", null),
            new CorrelatedBlock(Day.AddMinutes(40), Day.AddMinutes(60), "ULISTROIS-3428", null),
        };
        var correlation = new CorrelationResult(blocks, [], []);
        var events = SpanningEvents(Day, Day.AddMinutes(60));

        var result = DayTimelineBuilder.Build(events, correlation, [], []);

        Assert.Equal(2, result.Groups.Count);
        Assert.Equal((1, 2), (result.Groups[0].PlageIndex, result.Groups[0].PlageCount));
        Assert.Equal((2, 2), (result.Groups[1].PlageIndex, result.Groups[1].PlageCount));
    }

    [Fact]
    public void Build_TimeBlockStatusForKey_IsAppliedToGroup()
    {
        var block = new CorrelatedBlock(Day, Day.AddMinutes(20), "ULISTROIS-3428", null);
        var correlation = new CorrelationResult([block], [], []);
        var timeBlockRow = new TimeBlockRow(1, new TimeBlock(
            DateOnly.FromDateTime(Day), Day, Day.AddMinutes(20), "src", "ULISTROIS-3428",
            "POSID", "ZWPID", "note", 0.25, TimeBlockStatus.Validated, null));
        var events = SpanningEvents(Day, Day.AddMinutes(20));

        var result = DayTimelineBuilder.Build(events, correlation, [], [timeBlockRow]);

        Assert.Equal(TimeBlockStatus.Validated, Assert.Single(result.Groups).Status);
    }

    [Fact]
    public void Build_HourGrid_FloorsAndCeilsToWholeHours()
    {
        var start = new DateTime(2026, 8, 11, 8, 5, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 8, 11, 9, 0, 0, DateTimeKind.Utc);
        var block = new CorrelatedBlock(start, end, "T-1", null);
        var correlation = new CorrelationResult([block], [], []);
        var events = SpanningEvents(start, end);

        var result = DayTimelineBuilder.Build(events, correlation, [], []);

        Assert.Equal(0.0, result.Hours[0].Top);
        Assert.Equal(DayTimelineBuilder.HourHeightPx, result.Hours[1].Top);
        Assert.Equal(2, result.Hours.Count);
    }

    private static ActivityEvent[] SpanningEvents(DateTime startUtc, DateTime endUtc) =>
    [
        new ActivityEvent(1, startUtc, ActivityEventKind.Foreground, "proc", "titre", null),
        new ActivityEvent(2, endUtc, ActivityEventKind.IdleStart, null, null, null),
    ];
}
