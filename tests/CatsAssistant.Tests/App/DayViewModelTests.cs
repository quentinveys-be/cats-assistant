using CatsAssistant.App.ViewModels;
using CatsAssistant.Store;

namespace CatsAssistant.Tests.App;

public class DayViewModelTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Now);

    private static readonly TimeBlock ProposedLine = new(
        Today,
        new DateTime(2026, 8, 13, 9, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 8, 13, 10, 30, 0, DateTimeKind.Utc),
        "Daily standup",
        "ULISTROIS-3377",
        "P.ACSICAT01-01-P-0005",
        "ZS042",
        "ULISTROIS-3377 - Correctif",
        1.5,
        TimeBlockStatus.Proposed,
        null);

    private static readonly TimeBlock UncorrelatedLine = ProposedLine with
    {
        JiraKey = null,
        Note = "1:00 non corrélé sur 1 zone — ticket à renseigner",
        DurationHours = 1.0,
    };

    [Fact]
    public void Constructor_WithoutRepositories_StartsEmptyAndDoesNotThrow()
    {
        var viewModel = new DayViewModel();

        Assert.True(viewModel.IsEmpty);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.DayLabel));
        Assert.Empty(viewModel.Segments);
        Assert.Empty(viewModel.Lines);
        Assert.Equal(0, viewModel.ValidatedLinesCount);
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
    public void GoToSummaryCommand_InvokesNavigationCallback()
    {
        var invoked = false;
        var viewModel = new DayViewModel(navigateToSummary: () => invoked = true);

        viewModel.GoToSummaryCommand.Execute(null);

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

    [Fact]
    public void Constructor_LoadsTodaysLinesAndAggregates()
    {
        var repository = new FakeTimeBlockRepository();
        repository.Insert(ProposedLine);
        repository.Insert(UncorrelatedLine);
        repository.Insert(ProposedLine with { Date = Today.AddDays(-1) }); // autre jour, ignoré

        var viewModel = new DayViewModel(timeBlockRepository: repository);

        Assert.Equal(2, viewModel.Lines.Count);
        Assert.Equal("2:30", viewModel.TotalProposedLabel);
        Assert.Equal("7:36", viewModel.ExpectedLabel);
        Assert.True(viewModel.Lines.Single(l => l.IsUncorrelated).KeyDisplay == "Aucun ticket");
    }

    [Fact]
    public void ToggleValidateCommand_TogglesStatusAndPersists()
    {
        var repository = new FakeTimeBlockRepository();
        var id = repository.Insert(ProposedLine);
        var viewModel = new DayViewModel(timeBlockRepository: repository);
        var line = Assert.Single(viewModel.Lines);

        line.ToggleValidateCommand.Execute(null);

        Assert.Equal(TimeBlockStatus.Validated, line.Status);
        Assert.Equal(TimeBlockStatus.Validated, repository.GetById(id)!.TimeBlock.Status);
        Assert.Equal(1, viewModel.ValidatedLinesCount);

        line.ToggleValidateCommand.Execute(null);

        Assert.Equal(TimeBlockStatus.Proposed, line.Status);
        Assert.Equal(0, viewModel.ValidatedLinesCount);
    }

    [Fact]
    public void ValidateAllCommand_ValidatesEveryNonSubmittedLine()
    {
        var repository = new FakeTimeBlockRepository();
        repository.Insert(ProposedLine);
        repository.Insert(ProposedLine with { Status = TimeBlockStatus.Submitted, JiraKey = "ULISTROIS-1" });
        var viewModel = new DayViewModel(timeBlockRepository: repository);

        viewModel.ValidateAllCommand.Execute(null);

        Assert.All(viewModel.Lines.Where(l => l.Status != TimeBlockStatus.Submitted),
            l => Assert.Equal(TimeBlockStatus.Validated, l.Status));
        Assert.Equal(2, viewModel.ValidatedLinesCount);
    }

    [Fact]
    public void GaugePercent_ReflectsTotalOverExpected()
    {
        var repository = new FakeTimeBlockRepository();
        repository.Insert(ProposedLine with { DurationHours = 7.6 });
        var viewModel = new DayViewModel(timeBlockRepository: repository);

        Assert.Equal(100, viewModel.GaugePercent);
    }

    private sealed class FakeActivityEventRepository(IReadOnlyList<ActivityEvent> events) : IActivityEventRepository
    {
        public long Insert(DateTime timestampUtc, ActivityEventKind kind, string? process, string? windowTitle, string? url) =>
            throw new NotSupportedException();

        public IReadOnlyList<ActivityEvent> GetByDateRange(DateTime fromUtc, DateTime toUtc) => events;

        public void Delete(long id) => throw new NotSupportedException();

        public int DeleteOlderThan(DateTime thresholdUtc) => throw new NotSupportedException();

        public int Count() => throw new NotSupportedException();

        public int DeleteAll() => throw new NotSupportedException();
    }
}
