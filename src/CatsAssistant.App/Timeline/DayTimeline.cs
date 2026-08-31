using CatsAssistant.Store;

namespace CatsAssistant.App.Timeline;

public sealed record HourMark(string Label, double Top);

/// <summary>Un segment brut capté (activité ou inactivité), colonne gauche de la carte « Journée capturée ».</summary>
public sealed record TimelineSegment(
    DateTime StartLocal,
    DateTime EndLocal,
    string? Process,
    string? Detail,
    TimelineHue Hue,
    string? JiraKey,
    double Top,
    double Height,
    bool ShowLabels,
    bool ShowStartTime);

/// <summary>Une plage CATS regroupée (colonne droite) : un CorrelatedBlock attribué à un ticket.</summary>
public sealed record TimelineGroup(
    string JiraKey,
    DateTime StartLocal,
    DateTime EndLocal,
    TimelineHue Hue,
    TimeBlockStatus Status,
    int PlageIndex,
    int PlageCount,
    double Top,
    double Height,
    bool ShowMeta);

/// <summary>Une zone « à imputer » : un CorrelatedBlock non couvert par un regroupement CATS.</summary>
public sealed record TimelineGap(
    DateTime StartLocal,
    DateTime EndLocal,
    double Top,
    double Height,
    string? Label);

public sealed record TimelineMeeting(
    string Subject,
    DateTime StartLocal,
    DateTime EndLocal,
    double Top,
    double Height,
    bool ShowLabel);

public sealed record DayTimeline(
    IReadOnlyList<HourMark> Hours,
    IReadOnlyList<TimelineSegment> Segments,
    IReadOnlyList<TimelineGroup> Groups,
    IReadOnlyList<TimelineGap> Gaps,
    IReadOnlyList<TimelineMeeting> Meetings,
    double HeightPx,
    bool IsEmpty)
{
    public static readonly DayTimeline Empty = new([], [], [], [], [], 0, true);
}
