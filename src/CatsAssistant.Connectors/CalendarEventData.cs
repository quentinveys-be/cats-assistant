namespace CatsAssistant.Connectors;

public sealed record CalendarEventData(DateTime StartUtc, DateTime EndUtc, string Subject, string? Organizer);
