using CatsAssistant.Connectors;
using CatsAssistant.Correlator;
using CatsAssistant.Store;

namespace CatsAssistant.App.Timeline;

/// <summary>
/// Calcule la mise en page pixel de l'écran Journée (issue #17) à partir des données déjà produites par
/// le Correlator (issues #38/#44) : positions absolues, teintes, libellés conditionnels. Sans dépendance
/// WPF pour rester testable ; la vue convertit ensuite ces valeurs en Thickness/Brush.
/// </summary>
public static class DayTimelineBuilder
{
    public const double HourHeightPx = 56.0;

    public static DayTimeline Build(
        IReadOnlyList<ActivityEvent> activityEvents,
        CorrelationResult correlation,
        IReadOnlyList<CalendarEventData> meetings,
        IReadOnlyList<TimeBlockRow> timeBlocksForDay)
    {
        var segments = ActivityEventAggregator.Aggregate(activityEvents);
        if (segments.Count == 0 && correlation.IdlePeriods.Count == 0 && meetings.Count == 0)
        {
            return DayTimeline.Empty;
        }

        var gridStart = FloorToHour(EarliestLocal(segments, correlation.IdlePeriods, meetings));
        var gridEnd = CeilToHour(LatestLocal(segments, correlation.IdlePeriods, meetings));
        if (gridEnd <= gridStart)
        {
            gridEnd = gridStart.AddHours(1);
        }

        double TopLocal(DateTime local) => (local - gridStart).TotalHours * HourHeightPx;
        double TopUtc(DateTime utc) => TopLocal(utc.ToLocalTime());

        var hours = new List<HourMark>();
        for (var hour = gridStart; hour <= gridEnd; hour = hour.AddHours(1))
        {
            hours.Add(new HourMark($"{hour.Hour}:00", TopLocal(hour)));
        }

        var hueByKey = AssignHues(correlation.Blocks);
        var statusByKey = BuildStatusLookup(timeBlocksForDay);

        var rawEntries = MergeRawEntries(segments, correlation.IdlePeriods);
        var timelineSegments = rawEntries
            .Select(entry => BuildSegment(entry, correlation.Blocks, hueByKey, meetings, TopUtc))
            .ToList();

        // Zones « à imputer » du corrélateur, moins celles déjà couvertes par une plage manuelle (un
        // time_block créé par le dialogue d'édition, issue #19) : une zone imputée disparaît de la timeline
        // et sa plage manuelle rejoint la colonne des plages CATS.
        var uncorrelatedBlocks = correlation.Blocks.Where(b => b.JiraKey is null && !b.NoAttribution).ToList();
        var manualRanges = timeBlocksForDay
            .Where(row => uncorrelatedBlocks.Any(b => Overlaps(row.TimeBlock, b)))
            .ToList();

        var jiraBlocks = correlation.Blocks.Where(b => b.JiraKey is not null).ToList();
        var groupSpecs = jiraBlocks
            .Select(b => (Key: b.JiraKey!, b.StartUtc, b.EndUtc,
                Status: statusByKey.GetValueOrDefault(b.JiraKey!, TimeBlockStatus.Proposed)))
            .Concat(manualRanges.Select(r => (Key: r.TimeBlock.JiraKey ?? "Sans ticket",
                r.TimeBlock.StartUtc, r.TimeBlock.EndUtc, r.TimeBlock.Status)))
            .OrderBy(s => s.StartUtc)
            .ToList();

        foreach (var spec in groupSpecs.Where(s => !hueByKey.ContainsKey(s.Key)))
        {
            hueByKey[spec.Key] = Hues[hueByKey.Count % Hues.Length];
        }

        var plageCountByKey = groupSpecs.GroupBy(s => s.Key).ToDictionary(g => g.Key, g => g.Count());
        var plageSeenByKey = new Dictionary<string, int>();
        var groups = groupSpecs.Select(spec =>
        {
            plageSeenByKey[spec.Key] = plageSeenByKey.GetValueOrDefault(spec.Key) + 1;
            var rawHeight = TopUtc(spec.EndUtc) - TopUtc(spec.StartUtc);
            return new TimelineGroup(
                spec.Key,
                spec.StartUtc.ToLocalTime(),
                spec.EndUtc.ToLocalTime(),
                hueByKey[spec.Key],
                spec.Status,
                plageSeenByKey[spec.Key],
                plageCountByKey[spec.Key],
                TopUtc(spec.StartUtc) + 1,
                Math.Max(1, rawHeight - 3),
                rawHeight >= 28);
        }).ToList();

        var gaps = uncorrelatedBlocks
            .Where(b => !manualRanges.Any(r => Overlaps(r.TimeBlock, b)))
            .Select(block =>
            {
                var rawHeight = TopUtc(block.EndUtc) - TopUtc(block.StartUtc);
                return new TimelineGap(
                    block.StartUtc.ToLocalTime(),
                    block.EndUtc.ToLocalTime(),
                    TopUtc(block.StartUtc) + 1,
                    Math.Max(1, rawHeight - 3),
                    rawHeight >= 15 ? $"à imputer · {FormatDuration(block.EndUtc - block.StartUtc)}" : null);
            })
            .ToList();

        var timelineMeetings = meetings
            .OrderBy(m => m.StartUtc)
            .Select(meeting =>
            {
                var rawHeight = TopUtc(meeting.EndUtc) - TopUtc(meeting.StartUtc);
                return new TimelineMeeting(
                    meeting.Subject,
                    meeting.StartUtc.ToLocalTime(),
                    meeting.EndUtc.ToLocalTime(),
                    TopUtc(meeting.StartUtc) + 2,
                    Math.Max(1, rawHeight - 5),
                    rawHeight >= 22);
            })
            .ToList();

        return new DayTimeline(hours, timelineSegments, groups, gaps, timelineMeetings, TopLocal(gridEnd) + 8, IsEmpty: false);
    }

    private static TimelineSegment BuildSegment(
        (DateTime StartUtc, DateTime EndUtc, string? Process, string? Detail, bool IsIdle) entry,
        IReadOnlyList<CorrelatedBlock> blocks,
        IReadOnlyDictionary<string, TimelineHue> hueByKey,
        IReadOnlyList<CalendarEventData> meetings,
        Func<DateTime, double> topUtc)
    {
        var jiraKey = entry.IsIdle ? null : KeyOf(entry, blocks);
        var hue = entry.IsIdle
            ? TimelineHue.Idle
            : jiraKey is not null ? hueByKey[jiraKey] : TimelineHue.Uncorrelated;
        var rawHeight = topUtc(entry.EndUtc) - topUtc(entry.StartUtc);

        // Un segment ne masque son libellé que sous une réunion assez haute pour porter le sien
        // (docs/design/screens/cats-assistant.dc.html, calcul `under`).
        var under = meetings.Any(m =>
            entry.StartUtc < m.EndUtc && entry.EndUtc > m.StartUtc &&
            topUtc(m.EndUtc) - topUtc(m.StartUtc) >= 22);

        return new TimelineSegment(
            entry.StartUtc.ToLocalTime(),
            entry.EndUtc.ToLocalTime(),
            entry.Process,
            entry.Detail,
            hue,
            jiraKey,
            topUtc(entry.StartUtc),
            Math.Max(3, rawHeight - 1.5),
            ShowLabels: rawHeight >= 11 && !under,
            ShowStartTime: rawHeight >= 13 && !under);
    }

    private static string? KeyOf(
        (DateTime StartUtc, DateTime EndUtc, string? Process, string? Detail, bool IsIdle) entry,
        IReadOnlyList<CorrelatedBlock> blocks) =>
        blocks.FirstOrDefault(b => b.StartUtc <= entry.StartUtc && entry.EndUtc <= b.EndUtc)?.JiraKey;

    private static readonly TimelineHue[] Hues = [TimelineHue.Hue1, TimelineHue.Hue2, TimelineHue.Hue3, TimelineHue.Hue4];

    private static bool Overlaps(TimeBlock timeBlock, CorrelatedBlock block) =>
        timeBlock.StartUtc < block.EndUtc && block.StartUtc < timeBlock.EndUtc;

    private static Dictionary<string, TimelineHue> AssignHues(IReadOnlyList<CorrelatedBlock> blocks)
    {
        var map = new Dictionary<string, TimelineHue>();

        foreach (var block in blocks)
        {
            if (block.JiraKey is { } key && !map.ContainsKey(key))
            {
                map[key] = Hues[map.Count % Hues.Length];
            }
        }

        return map;
    }

    private static Dictionary<string, TimeBlockStatus> BuildStatusLookup(IReadOnlyList<TimeBlockRow> timeBlocksForDay) =>
        timeBlocksForDay
            .Where(row => row.TimeBlock.JiraKey is not null)
            .GroupBy(row => row.TimeBlock.JiraKey!)
            .ToDictionary(g => g.Key, g => g.First().TimeBlock.Status);

    private static List<(DateTime StartUtc, DateTime EndUtc, string? Process, string? Detail, bool IsIdle)> MergeRawEntries(
        IReadOnlyList<ActivitySegment> segments, IReadOnlyList<IdlePeriod> idlePeriods)
    {
        var entries = new List<(DateTime, DateTime, string?, string?, bool)>(segments.Count + idlePeriods.Count);
        entries.AddRange(segments.Select(s => (s.StartUtc, s.EndUtc, s.Process, s.WindowTitle, false)));
        entries.AddRange(idlePeriods.Select(p => (p.StartUtc, p.EndUtc, (string?)null, (string?)"Inactivité", true)));
        entries.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        return entries;
    }

    private static DateTime EarliestLocal(
        IReadOnlyList<ActivitySegment> segments, IReadOnlyList<IdlePeriod> idlePeriods, IReadOnlyList<CalendarEventData> meetings)
    {
        var min = DateTime.MaxValue;
        foreach (var s in segments) min = Min(min, s.StartUtc.ToLocalTime());
        foreach (var p in idlePeriods) min = Min(min, p.StartUtc.ToLocalTime());
        foreach (var m in meetings) min = Min(min, m.StartUtc.ToLocalTime());
        return min;
    }

    private static DateTime LatestLocal(
        IReadOnlyList<ActivitySegment> segments, IReadOnlyList<IdlePeriod> idlePeriods, IReadOnlyList<CalendarEventData> meetings)
    {
        var max = DateTime.MinValue;
        foreach (var s in segments) max = Max(max, s.EndUtc.ToLocalTime());
        foreach (var p in idlePeriods) max = Max(max, p.EndUtc.ToLocalTime());
        foreach (var m in meetings) max = Max(max, m.EndUtc.ToLocalTime());
        return max;
    }

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;

    private static DateTime FloorToHour(DateTime local) => local.Date.AddHours(local.Hour);

    private static DateTime CeilToHour(DateTime local) =>
        local.Minute == 0 && local.Second == 0 && local.Millisecond == 0
            ? local.Date.AddHours(local.Hour)
            : local.Date.AddHours(local.Hour + 1);

    private static string FormatDuration(TimeSpan span)
    {
        var totalMinutes = (int)Math.Round(span.TotalMinutes);
        return $"{totalMinutes / 60}:{totalMinutes % 60:00}";
    }
}
