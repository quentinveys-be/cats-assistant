using System.Text.RegularExpressions;
using CatsAssistant.Connectors;
using CatsAssistant.Store;

namespace CatsAssistant.Correlator;

/// <summary>
/// Applique les règles persistées (table rules) à un bloc non corrélé par la détection directe.
/// Première règle gagnante par priorité croissante (ordre déjà garanti par IRuleRepository.GetAll,
/// re-trié ici défensivement pour ne pas dépendre de l'ordre d'appel).
/// </summary>
public static class RuleEvaluator
{
    public static RuleEvaluation Evaluate(
        IReadOnlyList<ActivitySegment> segments,
        IReadOnlyList<string> urls,
        IReadOnlyList<VcsCommit> commitsInRange,
        IReadOnlyList<RuleRow> rules)
    {
        var warnings = new List<string>();

        foreach (var row in rules.OrderBy(r => r.Rule.Priority).ThenBy(r => r.Id))
        {
            if (Matches(row.Rule, segments, urls, commitsInRange, warnings))
            {
                return new RuleEvaluation(row.Rule.Target, warnings);
            }
        }

        return new RuleEvaluation(null, warnings);
    }

    private static bool Matches(
        Rule rule,
        IReadOnlyList<ActivitySegment> segments,
        IReadOnlyList<string> urls,
        IReadOnlyList<VcsCommit> commitsInRange,
        List<string> warnings) => rule.MatcherKind switch
    {
        RuleMatcherKind.Process => segments.Any(s => string.Equals(s.Process, rule.MatcherValue, StringComparison.OrdinalIgnoreCase)),
        RuleMatcherKind.TitleRegex => MatchesRegex(rule, segments.Select(s => s.WindowTitle), warnings),
        RuleMatcherKind.UrlRegex => MatchesRegex(rule, urls, warnings),
        RuleMatcherKind.JiraProject =>
            segments.Any(s => s.WindowTitle?.Contains(rule.MatcherValue, StringComparison.OrdinalIgnoreCase) == true) ||
            commitsInRange.Any(c => c.Branch.Contains(rule.MatcherValue, StringComparison.OrdinalIgnoreCase)),
        _ => false,
    };

    private static bool MatchesRegex(Rule rule, IEnumerable<string?> values, List<string> warnings)
    {
        Regex regex;
        try
        {
            regex = new Regex(rule.MatcherValue);
        }
        catch (ArgumentException ex)
        {
            warnings.Add($"Règle ignorée ({rule.MatcherKind}, matcher_value='{rule.MatcherValue}') : regex invalide - {ex.Message}");
            return false;
        }

        return values.Any(v => v is not null && regex.IsMatch(v));
    }
}
