using CatsAssistant.App.ViewModels;
using CatsAssistant.Store;

namespace CatsAssistant.Tests.App;

public class DayViewModelTests
{
    [Fact]
    public void Constructor_WithoutRepositories_StartsEmptyAndDoesNotThrow()
    {
        var viewModel = new DayViewModel();

        Assert.True(viewModel.IsEmpty);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.DayLabel));
        Assert.Empty(viewModel.Segments);
    }

    [Fact]
    public void NavigationCommands_ChangeDayLabel()
    {
        var viewModel = new DayViewModel();
        var initialLabel = viewModel.DayLabel;

        viewModel.PreviousDayCommand.Execute(null);
        var afterPrevious = viewModel.DayLabel;

        viewModel.NextDayCommand.Execute(null);
        viewModel.NextDayCommand.Execute(null);
        var afterNext = viewModel.DayLabel;

        Assert.NotEqual(initialLabel, afterPrevious);
        Assert.NotEqual(afterPrevious, afterNext);

        viewModel.TodayCommand.Execute(null);
        Assert.Equal(initialLabel, viewModel.DayLabel);
    }

    [Fact]
    public void GoToCatchUpCommand_WithoutCallback_CannotExecute()
    {
        var viewModel = new DayViewModel();

        Assert.False(viewModel.GoToCatchUpCommand.CanExecute(null));
    }

    [Fact]
    public void GoToCatchUpCommand_InvokesProvidedCallback()
    {
        var invoked = false;
        var viewModel = new DayViewModel(navigateToCatchUp: () => invoked = true);

        Assert.True(viewModel.GoToCatchUpCommand.CanExecute(null));
        viewModel.GoToCatchUpCommand.Execute(null);

        Assert.True(invoked);
    }

    [Fact]
    public void Constructor_WithActivityEvents_BuildsNonEmptyTimeline()
    {
        // Le fake ignore la plage demandée : seul le câblage DayViewModel -> DayTimelineBuilder est testé
        // ici, pas la précision du filtre par date (couverte par les tests des repositories SQLite).
        var repository = new FakeActivityEventRepository(
        [
            new ActivityEvent(1, DateTime.UtcNow, ActivityEventKind.Foreground, "chrome.exe", "sans ticket", null),
            new ActivityEvent(2, DateTime.UtcNow.AddMinutes(20), ActivityEventKind.IdleStart, null, null, null),
        ]);

        var viewModel = new DayViewModel(activityEventRepository: repository);

        Assert.False(viewModel.IsEmpty);
        Assert.NotEmpty(viewModel.Segments);
    }

    private sealed class FakeActivityEventRepository(IReadOnlyList<ActivityEvent> events) : IActivityEventRepository
    {
        public long Insert(DateTime timestampUtc, ActivityEventKind kind, string? process, string? windowTitle, string? url) =>
            throw new NotSupportedException();

        public IReadOnlyList<ActivityEvent> GetByDateRange(DateTime fromUtc, DateTime toUtc) => events;

        public void Delete(long id) => throw new NotSupportedException();

        public int DeleteOlderThan(DateTime thresholdUtc) => throw new NotSupportedException();
    }
}
