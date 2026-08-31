namespace CatsAssistant.Store;

/// <summary>Comptes affichés dans le dialogue de purge manuelle (issue #23) — <see cref="Preview"/> et
/// <see cref="ManualPurgeService.Purge"/> renvoient la même forme pour que le dialogue annonce exactement
/// ce qui sera (ou a été) supprimé.</summary>
public sealed record ManualPurgeSummary(int ActivityEvents, int UnsubmittedTimeBlocks, int LearnedRules);

/// <summary>
/// Purge manuelle (dette actée de la Phase 1, issue #23) : distincte de <see cref="ActivityEventRetentionPurger"/>
/// (purge automatique par ancienneté). Efface tout l'historique local reconstructible — événements d'activité,
/// blocs non soumis, règles apprises — sans jamais toucher aux blocs soumis (+ leur Counter SAP), aux règles
/// manuelles ni au coffre de secrets (hors de portée de cette classe).
/// </summary>
public sealed class ManualPurgeService
{
    private readonly IActivityEventRepository _events;
    private readonly ITimeBlockRepository _timeBlocks;
    private readonly IRuleRepository _rules;

    public ManualPurgeService(IActivityEventRepository events, ITimeBlockRepository timeBlocks, IRuleRepository rules)
    {
        _events = events;
        _timeBlocks = timeBlocks;
        _rules = rules;
    }

    public ManualPurgeSummary Preview() =>
        new(_events.Count(), _timeBlocks.CountUnsubmitted(), _rules.CountByOrigin(RuleOrigin.Learned));

    public ManualPurgeSummary Purge() =>
        new(_events.DeleteAll(), _timeBlocks.DeleteUnsubmitted(), _rules.DeleteByOrigin(RuleOrigin.Learned));
}
