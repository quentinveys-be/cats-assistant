namespace CatsAssistant.Connectors;

/// <summary>
/// DTO retourné par <see cref="IJiraConnector"/> — la persistance en base (jira_tickets) est du ressort
/// de l'étape 2.5 (repositories), pas de ce connecteur.
/// </summary>
public sealed record JiraTicket(
    string Key,
    string? Summary,
    string? Status,
    string? Context,
    string? ImputationCodeRaw,
    string? Posid,
    string? Zwpid,
    double? Effort);
