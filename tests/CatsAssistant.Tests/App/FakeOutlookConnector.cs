using CatsAssistant.Connectors;

namespace CatsAssistant.Tests.App;

internal sealed class FakeOutlookConnector : IOutlookConnector
{
    private readonly Func<int, IReadOnlyList<CalendarEventData>> _resultFactory;

    public FakeOutlookConnector(Func<int, IReadOnlyList<CalendarEventData>> resultFactory)
    {
        _resultFactory = resultFactory;
    }

    public FakeOutlookConnector(IReadOnlyList<CalendarEventData> result)
        : this(_ => result)
    {
    }

    public int CallCount { get; private set; }

    public IReadOnlyList<CalendarEventData> GetCalendarEvents(DateTime fromUtc, DateTime toUtc)
    {
        CallCount++;
        return _resultFactory(CallCount);
    }
}
