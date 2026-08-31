using System.Globalization;

namespace CatsAssistant.App;

internal static class SettingsInt
{
    public static int ParseOrDefault(string? raw, int fallback) =>
        int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
}
