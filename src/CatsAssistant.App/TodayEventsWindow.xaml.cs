using System.Windows;
using CatsAssistant.Store;

namespace CatsAssistant.App;

/// <summary>
/// Fenêtre minimale listant les activity_events du jour, non agrégés — la timeline agrégée arrive en Phase 3.
/// </summary>
public partial class TodayEventsWindow : Window
{
    public TodayEventsWindow(IActivityEventRepository repository)
    {
        InitializeComponent();

        var todayStartLocal = DateTime.Today;
        var todayEndLocal = todayStartLocal.AddDays(1);

        EventsGrid.ItemsSource = repository
            .GetByDateRange(todayStartLocal.ToUniversalTime(), todayEndLocal.ToUniversalTime())
            .Select(e => new TodayEventRow(
                e.TimestampUtc.ToLocalTime().ToString("HH:mm:ss"),
                e.Kind.ToString(),
                e.Process,
                e.WindowTitle))
            .ToList();
    }

    private sealed record TodayEventRow(string Time, string Kind, string? Process, string? WindowTitle);
}
