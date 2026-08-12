using System.Globalization;

namespace CatsAssistant.Connectors;

public static class OutlookRestrictFilterBuilder
{
    // Outlook's DASL Restrict() parses date literals using this exact format regardless of the Windows
    // regional settings of the machine running the automation. Using the current culture's short format
    // (e.g. dd/MM/yyyy on a French machine) silently swaps day and month for ambiguous dates, shifting the
    // queried window by up to several months.
    private const string OutlookDateFormat = "MM/dd/yyyy hh:mm tt";

    public static string BuildStartDateRangeFilter(DateTime fromLocal, DateTime toLocal)
    {
        var from = fromLocal.ToString(OutlookDateFormat, CultureInfo.InvariantCulture);
        var to = toLocal.ToString(OutlookDateFormat, CultureInfo.InvariantCulture);
        return $"[Start] >= '{from}' AND [Start] < '{to}'";
    }
}
