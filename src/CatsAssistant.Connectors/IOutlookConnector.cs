namespace CatsAssistant.Connectors;

/// <summary>
/// Reads calendar events from the local Outlook profile. Implementations must never read meeting
/// bodies or attachments (CLAUDE.md: no meeting content capture).
/// </summary>
public interface IOutlookConnector
{
    IReadOnlyList<CalendarEventData> GetCalendarEvents(DateTime fromUtc, DateTime toUtc);
}
