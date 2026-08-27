using CatsAssistant.Store;

namespace CatsAssistant.App.Services;

/// <summary>Résultat du calcul de rattrapage pour une journée (issue #22) : durée proposée, statut agrégé et
/// note explicative, plus les lignes CATS sous-jacentes (nécessaires pour valider la journée).</summary>
public sealed record CatchUpDayInfo(
    DateOnly Date,
    double ProposedHours,
    double ExpectedHours,
    CatchUpDayStatus Status,
    string Note,
    IReadOnlyList<TimeBlockRow> Blocks);
