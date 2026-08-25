using CatsAssistant.Connectors;
using CatsAssistant.Store;

namespace CatsAssistant.Correlator;

public interface ICorrelationEngine
{
    CorrelationResult Correlate(
        IReadOnlyList<ActivityEvent> activityEvents,
        IReadOnlyList<VcsCommit> commits,
        IReadOnlyList<CalendarEventData> meetings,
        int minBlockDurationMinutes = 15);
}
