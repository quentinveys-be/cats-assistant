namespace CatsAssistant.Correlator;

/// <summary>
/// JiraKey null signifie "non corrélé" (zone à imputer manuellement dans l'UI).
/// </summary>
public sealed record CorrelatedBlock(
    DateTime StartUtc,
    DateTime EndUtc,
    string? JiraKey,
    string? MeetingSubject);
