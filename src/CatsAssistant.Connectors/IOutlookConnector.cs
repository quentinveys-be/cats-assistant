namespace CatsAssistant.Connectors;

/// <summary>
/// Reads calendar events from the local Outlook profile. Implementations must never read meeting
/// bodies or attachments (CLAUDE.md: no meeting content capture).
/// </summary>
/// <remarks>
/// Outlook's COM automation server requires an STA thread; callers may invoke this from any thread
/// (background sync timer or UI) — <see cref="OutlookComConnector"/>/<see cref="OutlookComAppointmentSource"/>
/// transparently marshal the COM work onto a dedicated STA thread via <see cref="StaThreadRunner"/> when
/// called from a non-STA context.
/// </remarks>
public interface IOutlookConnector
{
    IReadOnlyList<CalendarEventData> GetCalendarEvents(DateTime fromUtc, DateTime toUtc);
}
