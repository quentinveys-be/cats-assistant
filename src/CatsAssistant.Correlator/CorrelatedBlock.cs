namespace CatsAssistant.Correlator;

/// <summary>
/// JiraKey null signifie "non corrélé" (zone à imputer manuellement dans l'UI), sauf si
/// NoAttribution est vrai : dans ce cas une règle a explicitement classé le bloc comme non facturable.
/// </summary>
public sealed record CorrelatedBlock(
    DateTime StartUtc,
    DateTime EndUtc,
    string? JiraKey,
    string? MeetingSubject,
    bool NoAttribution = false);
