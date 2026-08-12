namespace CatsAssistant.Connectors;

public static class CalendarEventMapper
{
    public static CalendarEventData Map(DateTime startLocal, DateTime endLocal, string? subject, string? organizer, TimeZoneInfo localTimeZone)
    {
        if (endLocal < startLocal)
        {
            throw new ArgumentException("L'heure de fin d'une réunion ne peut pas précéder son heure de début.", nameof(endLocal));
        }

        var startUtc = ToUtc(startLocal, localTimeZone);
        var endUtc = ToUtc(endLocal, localTimeZone);
        var normalizedSubject = string.IsNullOrWhiteSpace(subject) ? "(sans objet)" : subject.Trim();
        var normalizedOrganizer = string.IsNullOrWhiteSpace(organizer) ? null : organizer.Trim();

        return new CalendarEventData(startUtc, endUtc, normalizedSubject, normalizedOrganizer);
    }

    private static DateTime ToUtc(DateTime local, TimeZoneInfo localTimeZone)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, localTimeZone);
    }
}
