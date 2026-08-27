using CatsAssistant.Connectors;
using CatsAssistant.Correlator;
using CatsAssistant.Store;

namespace CatsAssistant.Tests.Correlator;

public class CorrelationEngineTests
{
    private static readonly DateTime Day = new(2026, 8, 10, 9, 0, 0, DateTimeKind.Utc);

    private readonly CorrelationEngine _engine = new();

    [Fact]
    public void Correlate_EmptyInput_ReturnsEmptyResult()
    {
        var result = _engine.Correlate(
            Array.Empty<ActivityEvent>(),
            Array.Empty<VcsCommit>(),
            Array.Empty<CalendarEventData>());

        Assert.Empty(result.Blocks);
        Assert.Empty(result.IdlePeriods);
    }

    [Fact]
    public void Correlate_SegmentAboveMinDuration_ProducesSingleBlock()
    {
        var events = new[]
        {
            new ActivityEvent(1, Day, ActivityEventKind.Foreground, "idea64.exe", "ULISTROIS-3101 - IntelliJ IDEA", null),
            new ActivityEvent(2, Day.AddMinutes(20), ActivityEventKind.TitleChange, "idea64.exe", "ULISTROIS-3101 - IntelliJ IDEA", null),
        };

        var result = _engine.Correlate(events, Array.Empty<VcsCommit>(), Array.Empty<CalendarEventData>());

        var block = Assert.Single(result.Blocks);
        Assert.Equal(Day, block.StartUtc);
        Assert.Equal(Day.AddMinutes(20), block.EndUtc);
        Assert.Equal("ULISTROIS-3101", block.JiraKey);
    }

    [Fact]
    public void Correlate_ShortTrailingSegment_MergesIntoContiguousPreviousBlock()
    {
        var events = new[]
        {
            new ActivityEvent(1, Day, ActivityEventKind.Foreground, "idea64.exe", "ULISTROIS-3101", null),
            new ActivityEvent(2, Day.AddMinutes(20), ActivityEventKind.Foreground, "chrome.exe", "sans ticket", null),
            new ActivityEvent(3, Day.AddMinutes(25), ActivityEventKind.IdleStart, null, null, null),
        };

        var result = _engine.Correlate(events, Array.Empty<VcsCommit>(), Array.Empty<CalendarEventData>(), minBlockDurationMinutes: 15);

        var block = Assert.Single(result.Blocks);
        Assert.Equal(Day, block.StartUtc);
        Assert.Equal(Day.AddMinutes(25), block.EndUtc);
        Assert.Equal("ULISTROIS-3101", block.JiraKey);
    }

    [Fact]
    public void Correlate_ShortSegmentIsolatedByIdleOnBothSides_StaysStandalone()
    {
        var events = new[]
        {
            new ActivityEvent(1, Day, ActivityEventKind.Foreground, "idea64.exe", "ULISTROIS-3101", null),
            new ActivityEvent(2, Day.AddMinutes(20), ActivityEventKind.IdleStart, null, null, null),
            new ActivityEvent(3, Day.AddMinutes(30), ActivityEventKind.IdleEnd, null, null, null),
            new ActivityEvent(4, Day.AddMinutes(30), ActivityEventKind.Foreground, "chrome.exe", "sans ticket", null),
            new ActivityEvent(5, Day.AddMinutes(35), ActivityEventKind.IdleStart, null, null, null),
        };

        var result = _engine.Correlate(events, Array.Empty<VcsCommit>(), Array.Empty<CalendarEventData>());

        Assert.Equal(2, result.Blocks.Count);
        Assert.Equal("ULISTROIS-3101", result.Blocks[0].JiraKey);
        Assert.Null(result.Blocks[1].JiraKey);
        Assert.Equal(Day.AddMinutes(30), result.Blocks[1].StartUtc);
        Assert.Equal(Day.AddMinutes(35), result.Blocks[1].EndUtc);
    }

    [Fact]
    public void Correlate_NoJiraKeyDetectable_MarksBlockUncorrelated()
    {
        var events = new[]
        {
            new ActivityEvent(1, Day, ActivityEventKind.Foreground, "chrome.exe", "Gmail", null),
            new ActivityEvent(2, Day.AddMinutes(20), ActivityEventKind.TitleChange, "chrome.exe", "Gmail", null),
        };

        var result = _engine.Correlate(events, Array.Empty<VcsCommit>(), Array.Empty<CalendarEventData>());

        var block = Assert.Single(result.Blocks);
        Assert.Null(block.JiraKey);
    }

    [Fact]
    public void Correlate_JiraKeyFromCommit_UsedWhenWindowTitleHasNone()
    {
        var events = new[]
        {
            new ActivityEvent(1, Day, ActivityEventKind.Foreground, "idea64.exe", "sans ticket", null),
            new ActivityEvent(2, Day.AddMinutes(20), ActivityEventKind.TitleChange, "idea64.exe", "sans ticket", null),
        };
        var commits = new[]
        {
            new VcsCommit("abc123", new DateTimeOffset(Day.AddMinutes(10)), "repo", "ULISTROIS/3101", "wip", "ULISTROIS-3101"),
        };

        var result = _engine.Correlate(events, commits, Array.Empty<CalendarEventData>());

        var block = Assert.Single(result.Blocks);
        Assert.Equal("ULISTROIS-3101", block.JiraKey);
    }

    [Fact]
    public void Correlate_CommitOutsideBlockRange_IsIgnored()
    {
        var events = new[]
        {
            new ActivityEvent(1, Day, ActivityEventKind.Foreground, "idea64.exe", "sans ticket", null),
            new ActivityEvent(2, Day.AddMinutes(20), ActivityEventKind.TitleChange, "idea64.exe", "sans ticket", null),
        };
        var commits = new[]
        {
            new VcsCommit("abc123", new DateTimeOffset(Day.AddHours(5)), "repo", "ULISTROIS/3101", "wip", "ULISTROIS-3101"),
        };

        var result = _engine.Correlate(events, commits, Array.Empty<CalendarEventData>());

        var block = Assert.Single(result.Blocks);
        Assert.Null(block.JiraKey);
    }

    [Fact]
    public void Correlate_OverlappingMeeting_AttachesSubjectToBlock()
    {
        var events = new[]
        {
            new ActivityEvent(1, Day, ActivityEventKind.Foreground, "teams.exe", "sans ticket", null),
            new ActivityEvent(2, Day.AddMinutes(20), ActivityEventKind.TitleChange, "teams.exe", "sans ticket", null),
        };
        var meetings = new[]
        {
            new CalendarEventData(Day.AddMinutes(5), Day.AddMinutes(15), "Daily standup", "chef@example.com"),
        };

        var result = _engine.Correlate(events, Array.Empty<VcsCommit>(), meetings);

        var block = Assert.Single(result.Blocks);
        Assert.Equal("Daily standup", block.MeetingSubject);
    }

    [Fact]
    public void Correlate_NonOverlappingMeeting_DoesNotAttachSubject()
    {
        var events = new[]
        {
            new ActivityEvent(1, Day, ActivityEventKind.Foreground, "teams.exe", "sans ticket", null),
            new ActivityEvent(2, Day.AddMinutes(20), ActivityEventKind.TitleChange, "teams.exe", "sans ticket", null),
        };
        var meetings = new[]
        {
            new CalendarEventData(Day.AddHours(3), Day.AddHours(4), "Autre réunion", null),
        };

        var result = _engine.Correlate(events, Array.Empty<VcsCommit>(), meetings);

        var block = Assert.Single(result.Blocks);
        Assert.Null(block.MeetingSubject);
    }

    [Fact]
    public void Correlate_ExtractsIdlePeriods_SeparatelyFromBlocks()
    {
        var idleStart = Day.AddMinutes(20);
        var idleEnd = Day.AddMinutes(35);
        var events = new[]
        {
            new ActivityEvent(1, Day, ActivityEventKind.Foreground, "idea64.exe", "ULISTROIS-3101", null),
            new ActivityEvent(2, idleStart, ActivityEventKind.IdleStart, null, null, null),
            new ActivityEvent(3, idleEnd, ActivityEventKind.IdleEnd, null, null, null),
            new ActivityEvent(4, idleEnd, ActivityEventKind.Foreground, "idea64.exe", "ULISTROIS-3101", null),
            new ActivityEvent(5, idleEnd.AddMinutes(20), ActivityEventKind.TitleChange, "idea64.exe", "ULISTROIS-3101", null),
        };

        var result = _engine.Correlate(events, Array.Empty<VcsCommit>(), Array.Empty<CalendarEventData>());

        var idlePeriod = Assert.Single(result.IdlePeriods);
        Assert.Equal(new IdlePeriod(idleStart, idleEnd), idlePeriod);
        Assert.Equal(2, result.Blocks.Count);
        Assert.DoesNotContain(result.Blocks, b => b.StartUtc <= idleStart && b.EndUtc >= idleEnd);
    }

    [Fact]
    public void Correlate_UnmatchedIdleStart_IsIgnored()
    {
        var events = new[]
        {
            new ActivityEvent(1, Day, ActivityEventKind.Foreground, "idea64.exe", "ULISTROIS-3101", null),
            new ActivityEvent(2, Day.AddMinutes(20), ActivityEventKind.IdleStart, null, null, null),
        };

        var result = _engine.Correlate(events, Array.Empty<VcsCommit>(), Array.Empty<CalendarEventData>());

        Assert.Empty(result.IdlePeriods);
    }

    [Fact]
    public void Correlate_ConfigurableMinBlockDuration_RespectsCustomThreshold()
    {
        // Deux segments contigus de 20 min chacun (bascule de titre à t+20).
        var events = new[]
        {
            new ActivityEvent(1, Day, ActivityEventKind.Foreground, "idea64.exe", "ULISTROIS-3101", null),
            new ActivityEvent(2, Day.AddMinutes(20), ActivityEventKind.Foreground, "idea64.exe", "ULISTROIS-3102", null),
            new ActivityEvent(3, Day.AddMinutes(40), ActivityEventKind.TitleChange, "idea64.exe", "ULISTROIS-3102", null),
        };

        // Seuil 15 min : chaque segment de 20 min dépasse déjà le seuil -> 2 blocs distincts.
        var resultWith15MinThreshold = _engine.Correlate(events, Array.Empty<VcsCommit>(), Array.Empty<CalendarEventData>(), minBlockDurationMinutes: 15);
        Assert.Equal(2, resultWith15MinThreshold.Blocks.Count);
        Assert.Equal("ULISTROIS-3101", resultWith15MinThreshold.Blocks[0].JiraKey);
        Assert.Equal("ULISTROIS-3102", resultWith15MinThreshold.Blocks[1].JiraKey);

        // Seuil 30 min : un segment seul (20 min) n'atteint pas le seuil -> fusion avec le voisin contigu -> 1 bloc.
        var resultWith30MinThreshold = _engine.Correlate(events, Array.Empty<VcsCommit>(), Array.Empty<CalendarEventData>(), minBlockDurationMinutes: 30);
        var block = Assert.Single(resultWith30MinThreshold.Blocks);
        Assert.Equal(Day, block.StartUtc);
        Assert.Equal(Day.AddMinutes(40), block.EndUtc);
    }

    [Fact]
    public void Correlate_TitleRegexRule_ReclassifiesUncorrelatedBlock()
    {
        var events = new[]
        {
            new ActivityEvent(1, Day, ActivityEventKind.Foreground, "teams.exe", "Teams - Revue de sprint", null),
            new ActivityEvent(2, Day.AddMinutes(20), ActivityEventKind.TitleChange, "teams.exe", "Teams - Revue de sprint", null),
        };
        var rules = new[]
        {
            new RuleRow(1, new Rule(RuleMatcherKind.TitleRegex, "Teams.*Revue de sprint", "ULISTROIS-3390", 25, RuleOrigin.Learned)),
        };

        var result = _engine.Correlate(events, Array.Empty<VcsCommit>(), Array.Empty<CalendarEventData>(), rules: rules);

        var block = Assert.Single(result.Blocks);
        Assert.Equal("ULISTROIS-3390", block.JiraKey);
    }

    [Fact]
    public void Correlate_InvalidRegexRule_IgnoredWithoutCrashing_AndReportedAsWarning()
    {
        var events = new[]
        {
            new ActivityEvent(1, Day, ActivityEventKind.Foreground, "chrome.exe", "Gmail", null),
            new ActivityEvent(2, Day.AddMinutes(20), ActivityEventKind.TitleChange, "chrome.exe", "Gmail", null),
        };
        var rules = new[]
        {
            new RuleRow(1, new Rule(RuleMatcherKind.TitleRegex, "(unclosed[", "ULISTROIS-9999", 10, RuleOrigin.Manual)),
        };

        var result = _engine.Correlate(events, Array.Empty<VcsCommit>(), Array.Empty<CalendarEventData>(), rules: rules);

        var block = Assert.Single(result.Blocks);
        Assert.Null(block.JiraKey);
        Assert.Single(result.RuleWarnings);
    }

    [Fact]
    public void Correlate_ConflictingRules_LowestPriorityValueWinsFirst()
    {
        var events = new[]
        {
            new ActivityEvent(1, Day, ActivityEventKind.Foreground, "idea64.exe", "sans ticket", null),
            new ActivityEvent(2, Day.AddMinutes(20), ActivityEventKind.TitleChange, "idea64.exe", "sans ticket", null),
        };
        var rules = new[]
        {
            new RuleRow(1, new Rule(RuleMatcherKind.Process, "idea64.exe", "ULISTROIS-1111", 50, RuleOrigin.Manual)),
            new RuleRow(2, new Rule(RuleMatcherKind.Process, "idea64.exe", "ULISTROIS-2222", 20, RuleOrigin.Learned)),
        };

        var result = _engine.Correlate(events, Array.Empty<VcsCommit>(), Array.Empty<CalendarEventData>(), rules: rules);

        var block = Assert.Single(result.Blocks);
        Assert.Equal("ULISTROIS-2222", block.JiraKey);
    }

    [Fact]
    public void Correlate_DirectDetectionSucceeds_RulesAreNotConsulted()
    {
        var events = new[]
        {
            new ActivityEvent(1, Day, ActivityEventKind.Foreground, "chrome.exe", "sans ticket", null),
            new ActivityEvent(2, Day.AddMinutes(20), ActivityEventKind.TitleChange, "chrome.exe", "sans ticket", null),
        };
        var rules = new[]
        {
            new RuleRow(1, new Rule(RuleMatcherKind.Process, "chrome.exe", "(unparsable[", 10, RuleOrigin.Manual)),
        };
        var commits = new[]
        {
            new VcsCommit("abc123", new DateTimeOffset(Day.AddMinutes(10)), "repo", "ULISTROIS/3101", "wip", "ULISTROIS-3101"),
        };

        var result = _engine.Correlate(events, commits, Array.Empty<CalendarEventData>(), rules: rules);

        var block = Assert.Single(result.Blocks);
        Assert.Equal("ULISTROIS-3101", block.JiraKey);
        Assert.Empty(result.RuleWarnings);
    }

    [Fact]
    public void Correlate_LastActiveTicketTarget_UsesPreviousCorrelatedBlockKey()
    {
        var events = new[]
        {
            new ActivityEvent(1, Day, ActivityEventKind.Foreground, "idea64.exe", "ULISTROIS-3101", null),
            new ActivityEvent(2, Day.AddMinutes(20), ActivityEventKind.IdleStart, null, null, null),
            new ActivityEvent(3, Day.AddMinutes(30), ActivityEventKind.IdleEnd, null, null, null),
            new ActivityEvent(4, Day.AddMinutes(30), ActivityEventKind.Foreground, "outlook.exe", "sans ticket", null),
            new ActivityEvent(5, Day.AddMinutes(50), ActivityEventKind.TitleChange, "outlook.exe", "sans ticket", null),
        };
        var rules = new[]
        {
            new RuleRow(1, new Rule(RuleMatcherKind.Process, "outlook.exe", RuleTargets.LastActiveTicket, 20, RuleOrigin.Manual)),
        };

        var result = _engine.Correlate(events, Array.Empty<VcsCommit>(), Array.Empty<CalendarEventData>(), rules: rules);

        Assert.Equal(2, result.Blocks.Count);
        Assert.Equal("ULISTROIS-3101", result.Blocks[1].JiraKey);
    }

    [Fact]
    public void Correlate_NoAttributionTarget_MarksBlockNonBillableWithoutJiraKey()
    {
        var events = new[]
        {
            new ActivityEvent(1, Day, ActivityEventKind.Foreground, "outlook.exe", "sans ticket", null),
            new ActivityEvent(2, Day.AddMinutes(20), ActivityEventKind.TitleChange, "outlook.exe", "sans ticket", null),
        };
        var rules = new[]
        {
            new RuleRow(1, new Rule(RuleMatcherKind.Process, "outlook.exe", RuleTargets.NoAttribution, 40, RuleOrigin.Manual)),
        };

        var result = _engine.Correlate(events, Array.Empty<VcsCommit>(), Array.Empty<CalendarEventData>(), rules: rules);

        var block = Assert.Single(result.Blocks);
        Assert.Null(block.JiraKey);
        Assert.True(block.NoAttribution);
    }
}
