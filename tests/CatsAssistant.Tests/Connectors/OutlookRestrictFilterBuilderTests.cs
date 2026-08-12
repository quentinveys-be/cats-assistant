using CatsAssistant.Connectors;

namespace CatsAssistant.Tests.Connectors;

public class OutlookRestrictFilterBuilderTests
{
    [Fact]
    public void BuildStartDateRangeFilter_UsesInvariantMonthDayYearOrder_NotCurrentCultureShortFormat()
    {
        // 2026-08-11 (August, day 11): under a dd/MM/yyyy culture this would render "11/08/2026" —
        // the Outlook Restrict() parser must always see "08/11/2026" regardless of the host locale.
        var from = new DateTime(2026, 8, 11, 14, 0, 0);
        var to = new DateTime(2026, 8, 12, 0, 0, 0);

        var filter = OutlookRestrictFilterBuilder.BuildStartDateRangeFilter(from, to);

        Assert.Equal("[Start] >= '08/11/2026 02:00 PM' AND [Start] < '08/12/2026 12:00 AM'", filter);
    }

    [Theory]
    [InlineData(0, "12:00 AM")]
    [InlineData(11, "11:00 AM")]
    [InlineData(12, "12:00 PM")]
    [InlineData(13, "01:00 PM")]
    [InlineData(23, "11:00 PM")]
    public void BuildStartDateRangeFilter_FormatsHourAsTwelveHourClockWithAmPm(int hour, string expectedTime)
    {
        var from = new DateTime(2026, 1, 5, hour, 0, 0);
        var to = from.AddHours(1);

        var filter = OutlookRestrictFilterBuilder.BuildStartDateRangeFilter(from, to);

        Assert.Contains($"01/05/2026 {expectedTime}", filter);
    }
}
