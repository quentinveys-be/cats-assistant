using System.Text.RegularExpressions;

namespace CatsAssistant.Connectors;

public static partial class PosidZwpidExtractor
{
    public static PosidZwpidExtractionResult Extract(string? codeImputationValue)
    {
        if (string.IsNullOrEmpty(codeImputationValue))
        {
            return PosidZwpidExtractionResult.NotExtracted();
        }

        var match = CodePattern().Match(codeImputationValue);

        return match.Success
            ? PosidZwpidExtractionResult.Extracted(match.Groups[1].Value, match.Groups[2].Value)
            : PosidZwpidExtractionResult.NotExtracted();
    }

    [GeneratedRegex(@"\(([A-Z0-9.\-]+)\s+([A-Z0-9]+)\)\s*$")]
    private static partial Regex CodePattern();
}
