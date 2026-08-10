namespace CatsAssistant.Store;

public static class ActivityEventAggregator
{
    public static IReadOnlyList<ActivitySegment> Aggregate(IReadOnlyList<ActivityEvent> events)
    {
        var segments = new List<ActivitySegment>();
        ActivityEvent? segmentStart = null;
        ActivityEvent? lastActivityEvent = null;

        void CloseSegment(DateTime endUtc)
        {
            if (segmentStart is null)
            {
                return;
            }

            segments.Add(new ActivitySegment(segmentStart.TimestampUtc, endUtc, segmentStart.Process, segmentStart.WindowTitle));
            segmentStart = null;
            lastActivityEvent = null;
        }

        foreach (var evt in events)
        {
            switch (evt.Kind)
            {
                case ActivityEventKind.IdleStart:
                    CloseSegment(evt.TimestampUtc);
                    break;

                case ActivityEventKind.IdleEnd:
                    break;

                case ActivityEventKind.Foreground:
                case ActivityEventKind.TitleChange:
                    if (segmentStart is not null && segmentStart.Process == evt.Process && segmentStart.WindowTitle == evt.WindowTitle)
                    {
                        lastActivityEvent = evt;
                    }
                    else
                    {
                        CloseSegment(evt.TimestampUtc);
                        segmentStart = evt;
                        lastActivityEvent = evt;
                    }
                    break;
            }
        }

        if (segmentStart is not null)
        {
            segments.Add(new ActivitySegment(segmentStart.TimestampUtc, lastActivityEvent!.TimestampUtc, segmentStart.Process, segmentStart.WindowTitle));
        }

        return segments;
    }
}
