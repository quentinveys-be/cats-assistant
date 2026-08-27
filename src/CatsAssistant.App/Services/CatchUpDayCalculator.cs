using CatsAssistant.Store;

namespace CatsAssistant.App.Services;

/// <summary>
/// Détecte les jours ouvrés passés non complétés et calcule leur statut agrégé (issue #22, tâche 1).
/// Pure lecture/calcul : la persistance des validations passe par <see cref="ValidateDay"/>.
/// </summary>
public static class CatchUpDayCalculator
{
    // ponytail: seuil de couverture fixé à 85 % (aligné sur les couleurs de jauge de la maquette :
    // pct>=85 => attention, sinon erreur) pour distinguer "incomplet" (peu d'activité captée) de
    // "à vérifier" (durée quasi complète mais donnée à corriger). À recalibrer si le produit le demande.
    private const double MinimumCoverageRatio = 0.85;

    // ponytail: la remontée s'arrête au premier jour ouvré sans aucune ligne CATS (rien à rattraper avant
    // ce point — journée hors suivi, congé, ou avant l'installation de l'app) ou déjà validé, avec un
    // plafond de 60 jours civils comme garde-fou (activité proposée en continu, jamais validée). Sera
    // affiné par WorkCalendars SAP (Phase 4, non bloquant, cf. issue #22).
    private const int MaxLookbackDays = 60;

    public static IReadOnlyList<CatchUpDayInfo> ComputeIncompleteDays(
        ITimeBlockRepository repository, DateOnly today, double expectedHoursPerDay)
    {
        var days = new List<CatchUpDayInfo>();
        var cursor = today.AddDays(-1);

        for (var i = 0; i < MaxLookbackDays; i++, cursor = cursor.AddDays(-1))
        {
            if (!IsBusinessDay(cursor))
            {
                continue;
            }

            var info = ComputeDay(repository, cursor, expectedHoursPerDay, isToday: false);
            if (info.Blocks.Count == 0 || info.Status == CatchUpDayStatus.Validated)
            {
                break;
            }

            days.Add(info);
        }

        days.Reverse();

        if (IsBusinessDay(today))
        {
            var todayInfo = ComputeDay(repository, today, expectedHoursPerDay, isToday: true);
            if (todayInfo.Blocks.Count > 0)
            {
                days.Add(todayInfo);
            }
        }

        return days;
    }

    /// <summary>Valide une journée : les lignes proposées/éditées passent à Validated. Les lignes déjà
    /// soumises à SAP ne sont pas rétrogradées (le rattrapage valide, il ne soumet pas — hors périmètre).</summary>
    public static void ValidateDay(ITimeBlockRepository repository, IReadOnlyList<TimeBlockRow> blocks)
    {
        foreach (var row in blocks)
        {
            if (row.TimeBlock.Status is TimeBlockStatus.Submitted or TimeBlockStatus.Validated)
            {
                continue;
            }

            repository.Update(row.Id, row.TimeBlock with { Status = TimeBlockStatus.Validated });
        }
    }

    private static CatchUpDayInfo ComputeDay(ITimeBlockRepository repository, DateOnly date, double expectedHours, bool isToday)
    {
        var blocks = repository.GetByDateRange(date, date);
        var proposedHours = blocks.Sum(b => b.TimeBlock.DurationHours);

        if (isToday)
        {
            var submittedCount = blocks.Count(b => b.TimeBlock.Status == TimeBlockStatus.Submitted);
            var note = submittedCount switch
            {
                0 => "Journée en cours",
                1 => "Journée en cours · 1 ligne déjà soumise",
                _ => $"Journée en cours · {submittedCount} lignes déjà soumises",
            };
            return new CatchUpDayInfo(date, proposedHours, expectedHours, CatchUpDayStatus.InProgress, note, blocks);
        }

        if (proposedHours < expectedHours * MinimumCoverageRatio)
        {
            var gap = expectedHours - proposedHours;
            return new CatchUpDayInfo(date, proposedHours, expectedHours, CatchUpDayStatus.Incomplete,
                $"{FormatHours(gap)} d'activité non corrélée", blocks);
        }

        var missingTicketCount = blocks.Count(b => string.IsNullOrEmpty(b.TimeBlock.JiraKey));
        if (missingTicketCount > 0)
        {
            var note = missingTicketCount == 1 ? "1 ligne sans ticket JIRA" : $"{missingTicketCount} lignes sans ticket JIRA";
            return new CatchUpDayInfo(date, proposedHours, expectedHours, CatchUpDayStatus.NeedsReview, note, blocks);
        }

        if (blocks.All(b => b.TimeBlock.Status is TimeBlockStatus.Validated or TimeBlockStatus.Submitted))
        {
            return new CatchUpDayInfo(date, proposedHours, expectedHours, CatchUpDayStatus.Validated,
                $"{blocks.Count} lignes validées", blocks);
        }

        var readyNote = blocks.Count == 1 ? "1 ligne proposée, code vérifié" : $"{blocks.Count} lignes proposées, codes vérifiés";
        return new CatchUpDayInfo(date, proposedHours, expectedHours, CatchUpDayStatus.ReadyToValidate, readyNote, blocks);
    }

    public static bool IsBusinessDay(DateOnly date) =>
        date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    /// <summary>Formate une durée en heures décimales au format h:mm (ex. 7.6 → "7:36").</summary>
    public static string FormatHours(double hours)
    {
        var totalMinutes = (int)Math.Round(hours * 60, MidpointRounding.AwayFromZero);
        var sign = totalMinutes < 0 ? "-" : string.Empty;
        totalMinutes = Math.Abs(totalMinutes);
        return $"{sign}{totalMinutes / 60}:{totalMinutes % 60:D2}";
    }
}
