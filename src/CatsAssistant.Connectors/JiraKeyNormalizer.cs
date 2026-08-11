using System.Text.RegularExpressions;

namespace CatsAssistant.Connectors;

public static partial class JiraKeyNormalizer
{
    public static bool TryNormalize(string? input, out string normalizedKey)
    {
        if (!string.IsNullOrEmpty(input))
        {
            var match = KeyPattern().Match(input);
            if (match.Success)
            {
                normalizedKey = $"ULISTROIS-{match.Groups[1].Value}";
                return true;
            }
        }

        normalizedKey = string.Empty;
        return false;
    }

    [GeneratedRegex(@"ULISTROIS[-/](\d+)")]
    private static partial Regex KeyPattern();
}
