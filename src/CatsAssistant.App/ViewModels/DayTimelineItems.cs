using System.Windows;
using CatsAssistant.App.Timeline;
using CatsAssistant.Store;

namespace CatsAssistant.App.ViewModels;

/// <summary>
/// Enveloppes WPF (Thickness/Margin) autour des positions pixel pures de <see cref="DayTimelineBuilder"/>
/// (issue #17). Le calcul reste testable sans WPF ; seule cette conversion dépend de PresentationFramework.
/// </summary>
public sealed record HourMarkItem(string Label, Thickness LabelMargin, Thickness LineMargin)
{
    public static HourMarkItem From(HourMark mark) => new(
        mark.Label,
        new Thickness(0, mark.Top - 8, 0, 0),
        new Thickness(46, mark.Top, 0, 0));
}

public sealed record TimelineSegmentItem(
    Thickness Margin, double Height, TimelineHue Hue, string? Process, string? Detail,
    bool ShowLabels, bool ShowStartTime, string StartTimeLabel, string AutomationLabel,
    DateTime StartLocal, DateTime EndLocal, string? JiraKey)
{
    public static TimelineSegmentItem From(TimelineSegment segment) => new(
        new Thickness(46, segment.Top, 0, 0),
        segment.Height,
        segment.Hue,
        segment.Process,
        segment.Detail,
        segment.ShowLabels,
        segment.ShowStartTime,
        segment.StartLocal.ToString("HH:mm"),
        $"{segment.StartLocal:HH:mm}–{segment.EndLocal:HH:mm} · {segment.Process ?? "Inactivité"} · {segment.Detail}",
        segment.StartLocal,
        segment.EndLocal,
        segment.JiraKey);
}

public sealed record TimelineGroupItem(
    Thickness Margin, double Height, TimelineHue Hue, TimeBlockStatus Status,
    string Key, string DurationLabel, bool ShowMeta, string MetaLabel,
    DateTime StartLocal, DateTime EndLocal)
{
    public static TimelineGroupItem From(TimelineGroup group) => new(
        new Thickness(362, group.Top, 6, 0),
        group.Height,
        group.Hue,
        group.Status,
        group.JiraKey,
        FormatDuration(group.EndLocal - group.StartLocal),
        group.ShowMeta,
        $"{group.StartLocal:HH:mm}–{group.EndLocal:HH:mm}" +
            (group.PlageCount > 1 ? $" · plage {group.PlageIndex}/{group.PlageCount}" : string.Empty),
        group.StartLocal,
        group.EndLocal);

    public string AutomationLabel => $"Modifier la plage CATS {Key} · {MetaLabel}";

    private static string FormatDuration(TimeSpan span)
    {
        var totalMinutes = (int)Math.Round(span.TotalMinutes);
        return $"{totalMinutes / 60}:{totalMinutes % 60:00}";
    }
}

public sealed record TimelineGapItem(
    Thickness Margin, double Height, string? Label, DateTime StartLocal, DateTime EndLocal)
{
    public string AutomationLabel => $"Imputer cette plage · {StartLocal:HH:mm}–{EndLocal:HH:mm}";

    public static TimelineGapItem From(TimelineGap gap) => new(
        new Thickness(362, gap.Top, 6, 0),
        gap.Height,
        gap.Label,
        gap.StartLocal,
        gap.EndLocal);
}

public sealed record TimelineMeetingItem(Thickness Margin, double Height, string Subject, string TimeLabel, bool ShowLabel)
{
    public static TimelineMeetingItem From(TimelineMeeting meeting) => new(
        new Thickness(60, meeting.Top, 0, 0),
        meeting.Height,
        meeting.Subject,
        $"{meeting.StartLocal:HH:mm}–{meeting.EndLocal:HH:mm}",
        meeting.ShowLabel);
}
