using CatsAssistant.Connectors;
using CatsAssistant.Store;

namespace CatsAssistant.Correlator;

public sealed class CorrelationEngine : ICorrelationEngine
{
    public CorrelationResult Correlate(
        IReadOnlyList<ActivityEvent> activityEvents,
        IReadOnlyList<VcsCommit> commits,
        IReadOnlyList<CalendarEventData> meetings,
        int minBlockDurationMinutes = 15)
    {
        var minDuration = TimeSpan.FromMinutes(minBlockDurationMinutes);
        var idlePeriods = ExtractIdlePeriods(activityEvents);
        var segments = ActivityEventAggregator.Aggregate(activityEvents);
        var groups = GroupIntoBlocks(segments, minDuration);
        var blocks = groups.Select(g => BuildBlock(g, commits, meetings)).ToList();

        return new CorrelationResult(blocks, idlePeriods);
    }

    private static List<List<ActivitySegment>> GroupIntoBlocks(IReadOnlyList<ActivitySegment> segments, TimeSpan minDuration)
    {
        var groups = new List<List<ActivitySegment>>();
        List<ActivitySegment>? current = null;

        // Un "trou" entre deux segments ne peut être que de l'idle (ActivityEventAggregator ne
        // produit jamais de segments contigus autrement) : on ne fusionne jamais à travers un trou,
        // seule la fusion avec un voisin réellement contigu est autorisée.
        void FlushRemainder(List<ActivitySegment> remainder)
        {
            if (groups.Count > 0 && remainder[0].StartUtc == groups[^1][^1].EndUtc)
            {
                groups[^1].AddRange(remainder);
            }
            else
            {
                groups.Add(remainder);
            }
        }

        foreach (var segment in segments)
        {
            if (current is not null && segment.StartUtc > current[^1].EndUtc)
            {
                FlushRemainder(current);
                current = null;
            }

            current ??= new List<ActivitySegment>();
            current.Add(segment);

            if (current[^1].EndUtc - current[0].StartUtc >= minDuration)
            {
                groups.Add(current);
                current = null;
            }
        }

        if (current is not null)
        {
            FlushRemainder(current);
        }

        return groups;
    }

    private static CorrelatedBlock BuildBlock(
        List<ActivitySegment> segments,
        IReadOnlyList<VcsCommit> commits,
        IReadOnlyList<CalendarEventData> meetings)
    {
        var start = segments[0].StartUtc;
        var end = segments[^1].EndUtc;
        var jiraKey = DetectJiraKey(segments, commits, start, end);
        var meetingSubject = FindMeetingSubject(meetings, start, end);

        return new CorrelatedBlock(start, end, jiraKey, meetingSubject);
    }

    private static string? DetectJiraKey(
        IReadOnlyList<ActivitySegment> segments,
        IReadOnlyList<VcsCommit> commits,
        DateTime start,
        DateTime end)
    {
        foreach (var segment in segments)
        {
            if (JiraKeyNormalizer.TryNormalize(segment.WindowTitle, out var key))
            {
                return key;
            }
        }

        foreach (var commit in commits)
        {
            if (commit.JiraKey is not null && commit.TimestampUtc.UtcDateTime >= start && commit.TimestampUtc.UtcDateTime < end)
            {
                return commit.JiraKey;
            }
        }

        return null;
    }

    private static string? FindMeetingSubject(IReadOnlyList<CalendarEventData> meetings, DateTime start, DateTime end)
    {
        foreach (var meeting in meetings)
        {
            if (meeting.StartUtc < end && meeting.EndUtc > start)
            {
                return meeting.Subject;
            }
        }

        return null;
    }

    private static List<IdlePeriod> ExtractIdlePeriods(IReadOnlyList<ActivityEvent> events)
    {
        var periods = new List<IdlePeriod>();
        DateTime? idleStart = null;

        foreach (var evt in events)
        {
            switch (evt.Kind)
            {
                case ActivityEventKind.IdleStart:
                    idleStart = evt.TimestampUtc;
                    break;
                case ActivityEventKind.IdleEnd when idleStart is not null:
                    periods.Add(new IdlePeriod(idleStart.Value, evt.TimestampUtc));
                    idleStart = null;
                    break;
            }
        }

        // ponytail: un idle_start sans idle_end (encore inactif) n'a pas de fin connue -> ignoré
        // plutôt que deviné ; upgrade si l'UI a besoin d'afficher l'idle en cours.
        return periods;
    }
}
