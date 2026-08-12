namespace CatsAssistant.Connectors;

/// <summary>
/// Raw appointment data as read from Outlook, before UTC conversion/normalization. Only ever carries
/// subject, organizer and start/end — never the meeting body (CLAUDE.md: no meeting content capture).
/// </summary>
public sealed record OutlookAppointmentSnapshot(DateTime StartLocal, DateTime EndLocal, string? Subject, string? Organizer);
