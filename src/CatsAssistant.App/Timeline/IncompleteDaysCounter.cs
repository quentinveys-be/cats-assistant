using CatsAssistant.Store;

namespace CatsAssistant.App.Timeline;

/// <summary>Compte les jours ouvrés récents dont le total imputé n'atteint pas la durée attendue (issue #17,
/// bandeau « N jours non complétés »). Week-ends exclus faute de calendrier de congés.</summary>
public static class IncompleteDaysCounter
{
    public const double ExpectedDailyHours = 7.6;

    public static int CountIncompleteWeekdays(ITimeBlockRepository repository, DateOnly today, int lookbackDays = 14)
    {
        var count = 0;

        for (var offset = 1; offset <= lookbackDays; offset++)
        {
            var day = today.AddDays(-offset);
            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                continue;
            }

            var totalHours = repository.GetByDateRange(day, day).Sum(row => row.TimeBlock.DurationHours);
            if (totalHours < ExpectedDailyHours)
            {
                count++;
            }
        }

        return count;
    }
}
