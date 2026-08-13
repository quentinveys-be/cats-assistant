using CatsAssistant.Connectors;

namespace CatsAssistant.Store;

public interface ICalendarEventRepository
{
    long Insert(CalendarEventData calendarEvent);

    IReadOnlyList<CalendarEventData> GetByDateRange(DateTime fromUtc, DateTime toUtc);
}
