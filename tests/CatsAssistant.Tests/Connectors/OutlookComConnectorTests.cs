using CatsAssistant.Connectors;

namespace CatsAssistant.Tests.Connectors;

public class OutlookComConnectorTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    [Fact]
    public void GetCalendarEvents_MapsAppointmentsFromSourceToUtc()
    {
        var source = new FakeOutlookAppointmentSource(
            new OutlookAppointmentSnapshot(
                new DateTime(2026, 8, 11, 9, 0, 0),
                new DateTime(2026, 8, 11, 9, 30, 0),
                "Daily",
                "Alice Dupont"));
        var connector = new OutlookComConnector(source, Utc);

        var events = connector.GetCalendarEvents(
            new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 12, 0, 0, 0, DateTimeKind.Utc));

        var calendarEvent = Assert.Single(events);
        Assert.Equal("Daily", calendarEvent.Subject);
        Assert.Equal("Alice Dupont", calendarEvent.Organizer);
        Assert.Equal(new DateTime(2026, 8, 11, 9, 0, 0, DateTimeKind.Utc), calendarEvent.StartUtc);
    }

    [Fact]
    public void GetCalendarEvents_PassesLocalBoundsToSource()
    {
        var localTimeZone = TimeZoneInfo.CreateCustomTimeZone("Test/PlusTwo", TimeSpan.FromHours(2), "Test +02:00", "Test +02:00");
        var source = new FakeOutlookAppointmentSource();
        var connector = new OutlookComConnector(source, localTimeZone);

        connector.GetCalendarEvents(
            new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 12, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 8, 11, 2, 0, 0), source.LastFromLocal);
        Assert.Equal(new DateTime(2026, 8, 12, 2, 0, 0), source.LastToLocal);
    }

    [Fact]
    public void GetCalendarEvents_ReturnsResultsOrderedByStart()
    {
        var source = new FakeOutlookAppointmentSource(
            new OutlookAppointmentSnapshot(new DateTime(2026, 8, 11, 15, 0, 0), new DateTime(2026, 8, 11, 15, 30, 0), "Aprem", null),
            new OutlookAppointmentSnapshot(new DateTime(2026, 8, 11, 9, 0, 0), new DateTime(2026, 8, 11, 9, 30, 0), "Matin", null));
        var connector = new OutlookComConnector(source, Utc);

        var events = connector.GetCalendarEvents(
            new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 12, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new[] { "Matin", "Aprem" }, events.Select(e => e.Subject));
    }

    [Fact]
    public void GetCalendarEvents_NoAppointments_ReturnsEmptyList()
    {
        var source = new FakeOutlookAppointmentSource();
        var connector = new OutlookComConnector(source, Utc);

        var events = connector.GetCalendarEvents(
            new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 12, 0, 0, 0, DateTimeKind.Utc));

        Assert.Empty(events);
    }

    [Fact]
    public void GetCalendarEvents_ToBeforeFrom_Throws()
    {
        var connector = new OutlookComConnector(new FakeOutlookAppointmentSource(), Utc);

        Assert.Throws<ArgumentException>(() => connector.GetCalendarEvents(
            new DateTime(2026, 8, 12, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void GetCalendarEvents_SourceThrows_WrapsAsOutlookUnavailable()
    {
        var source = new FakeOutlookAppointmentSource { ThrowOnRead = new InvalidOperationException("COM error") };
        var connector = new OutlookComConnector(source, Utc);

        Assert.Throws<OutlookUnavailableException>(() => connector.GetCalendarEvents(
            new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 12, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void GetCalendarEvents_SourceThrowsOutlookUnavailable_PropagatesUnwrapped()
    {
        var original = new OutlookUnavailableException("Outlook n'est pas installé sur ce poste.");
        var source = new FakeOutlookAppointmentSource { ThrowOnRead = original };
        var connector = new OutlookComConnector(source, Utc);

        var thrown = Assert.Throws<OutlookUnavailableException>(() => connector.GetCalendarEvents(
            new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 12, 0, 0, 0, DateTimeKind.Utc)));
        Assert.Same(original, thrown);
    }

    private sealed class FakeOutlookAppointmentSource : IOutlookAppointmentSource
    {
        private readonly IReadOnlyList<OutlookAppointmentSnapshot> _snapshots;

        public FakeOutlookAppointmentSource(params OutlookAppointmentSnapshot[] snapshots)
        {
            _snapshots = snapshots;
        }

        public Exception? ThrowOnRead { get; init; }

        public DateTime? LastFromLocal { get; private set; }

        public DateTime? LastToLocal { get; private set; }

        public IReadOnlyList<OutlookAppointmentSnapshot> GetAppointments(DateTime fromLocal, DateTime toLocal)
        {
            LastFromLocal = fromLocal;
            LastToLocal = toLocal;

            if (ThrowOnRead is not null)
            {
                throw ThrowOnRead;
            }

            return _snapshots;
        }
    }
}
