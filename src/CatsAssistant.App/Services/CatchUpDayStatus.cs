namespace CatsAssistant.App.Services;

/// <summary>Statut agrégé d'une journée à l'écran Rattrapage (issue #22). Distinct de <see cref="CatsAssistant.Store.TimeBlockStatus"/>,
/// qui porte le cycle de vie d'une ligne CATS individuelle.</summary>
public enum CatchUpDayStatus
{
    Incomplete,
    NeedsReview,
    ReadyToValidate,
    InProgress,
    Validated,
}
