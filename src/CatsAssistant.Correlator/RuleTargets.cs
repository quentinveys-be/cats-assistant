namespace CatsAssistant.Correlator;

/// <summary>
/// Valeurs spéciales que la colonne rules.target peut porter en plus d'une clé JIRA explicite.
/// </summary>
public static class RuleTargets
{
    public const string LastActiveTicket = "LAST_ACTIVE_TICKET";
    public const string NoAttribution = "NO_ATTRIBUTION";
}
