namespace CatsAssistant.Connectors;

public sealed class OutlookComConnector : IOutlookConnector
{
    private readonly IOutlookAppointmentSource _appointmentSource;
    private readonly TimeZoneInfo _localTimeZone;

    public OutlookComConnector(IOutlookAppointmentSource? appointmentSource = null, TimeZoneInfo? localTimeZone = null)
    {
        _appointmentSource = appointmentSource ?? new OutlookComAppointmentSource();
        _localTimeZone = localTimeZone ?? TimeZoneInfo.Local;
    }

    public IReadOnlyList<CalendarEventData> GetCalendarEvents(DateTime fromUtc, DateTime toUtc)
    {
        if (toUtc < fromUtc)
        {
            throw new ArgumentException("La borne de fin doit être postérieure à la borne de début.", nameof(toUtc));
        }

        var fromLocal = TimeZoneInfo.ConvertTimeFromUtc(fromUtc, _localTimeZone);
        var toLocal = TimeZoneInfo.ConvertTimeFromUtc(toUtc, _localTimeZone);

        IReadOnlyList<OutlookAppointmentSnapshot> snapshots;
        try
        {
            snapshots = _appointmentSource.GetAppointments(fromLocal, toLocal);
        }
        catch (Exception ex) when (ex is not OutlookUnavailableException)
        {
            throw new OutlookUnavailableException("Outlook local est indisponible ou n'a pas de profil configuré.", ex);
        }

        return snapshots
            .Select(snapshot => CalendarEventMapper.Map(snapshot.StartLocal, snapshot.EndLocal, snapshot.Subject, snapshot.Organizer, _localTimeZone))
            .OrderBy(calendarEvent => calendarEvent.StartUtc)
            .ToList();
    }
}
