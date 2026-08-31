using CatsAssistant.App.Services;
using CatsAssistant.App.ViewModels;
using CatsAssistant.Store;

namespace CatsAssistant.Tests.App;

public class MainWindowViewModelTests
{
    [Fact]
    public void Constructor_SelectsDayScreenByDefault()
    {
        using var viewModel = new MainWindowViewModel(syncService: null);

        Assert.IsType<DayViewModel>(viewModel.CurrentScreen);
        Assert.True(viewModel.NavigationItems[0].IsSelected);
        Assert.All(viewModel.NavigationItems.Skip(1), item => Assert.False(item.IsSelected));
        Assert.False(viewModel.SettingsItem.IsSelected);
    }

    [Fact]
    public void SelectCommand_OnAnotherItem_SwitchesScreenAndSelection()
    {
        using var viewModel = new MainWindowViewModel(syncService: null);
        var catchUp = viewModel.NavigationItems.Single(i => i.Label == "Rattrapage");

        catchUp.SelectCommand.Execute(null);

        Assert.IsType<CatchUpViewModel>(viewModel.CurrentScreen);
        Assert.True(catchUp.IsSelected);
        Assert.All(viewModel.NavigationItems.Where(i => i != catchUp), item => Assert.False(item.IsSelected));
        Assert.False(viewModel.SettingsItem.IsSelected);
    }

    [Fact]
    public void SelectCommand_OnSettingsItem_NavigatesAndDeselectsMainItems()
    {
        using var viewModel = new MainWindowViewModel(syncService: null);

        viewModel.SettingsItem.SelectCommand.Execute(null);

        Assert.IsType<SettingsViewModel>(viewModel.CurrentScreen);
        Assert.True(viewModel.SettingsItem.IsSelected);
        Assert.All(viewModel.NavigationItems, item => Assert.False(item.IsSelected));
    }

    [Fact]
    public void ToggleThemeCommand_TogglesIsDarkTheme()
    {
        using var viewModel = new MainWindowViewModel(syncService: null);

        Assert.False(viewModel.IsDarkTheme);
        viewModel.ToggleThemeCommand.Execute(null);
        Assert.True(viewModel.IsDarkTheme);
        viewModel.ToggleThemeCommand.Execute(null);
        Assert.False(viewModel.IsDarkTheme);
    }

    [Fact]
    public void Constructor_WithoutTimeBlockRepository_SummaryBadgeStartsAtZero()
    {
        using var viewModel = new MainWindowViewModel(syncService: null);

        var summary = viewModel.NavigationItems.Single(i => i.Label == "Récapitulatif");
        Assert.Equal(0, summary.BadgeCount);
    }

    [Fact]
    public void DayScreen_GoToSummaryCommand_NavigatesToSummary()
    {
        using var viewModel = new MainWindowViewModel(syncService: null);
        var day = (DayViewModel)viewModel.CurrentScreen;

        day.GoToSummaryCommand.Execute(null);

        Assert.IsType<SummaryViewModel>(viewModel.CurrentScreen);
        Assert.True(viewModel.NavigationItems.Single(i => i.Label == "Récapitulatif").IsSelected);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var viewModel = new MainWindowViewModel(syncService: null);

        var exception = Record.Exception(viewModel.Dispose);

        Assert.Null(exception);
    }

    [Fact]
    public void Constructor_WithPersistedDarkTheme_StartsInDarkTheme()
    {
        var settings = new FakeSettingsRepository();
        settings.Set("ui.theme", "dark");

        using var viewModel = new MainWindowViewModel(syncService: null, settings);

        Assert.True(viewModel.IsDarkTheme);
    }

    [Fact]
    public void ToggleThemeCommand_PersistsChoice()
    {
        var settings = new FakeSettingsRepository();
        using var viewModel = new MainWindowViewModel(syncService: null, settings);

        viewModel.ToggleThemeCommand.Execute(null);
        Assert.Equal("dark", settings.Get("ui.theme"));

        viewModel.ToggleThemeCommand.Execute(null);
        Assert.Equal("light", settings.Get("ui.theme"));
    }

    [Fact]
    public void Constructor_WithIncompleteCatchUpDays_SetsNavigationBadge()
    {
        using var viewModel = new MainWindowViewModel(syncService: null, timeBlockRepository: RepositoryWithOneIncompleteDayBeforeToday());
        var catchUp = viewModel.NavigationItems.Single(i => i.Label == "Rattrapage");

        Assert.Equal(1, catchUp.BadgeCount);
    }

    [Fact]
    public void CatchUp_OpenDay_SwitchesToDayScreenWithSelectedDate()
    {
        using var viewModel = new MainWindowViewModel(syncService: null, timeBlockRepository: RepositoryWithOneIncompleteDayBeforeToday());
        var catchUp = (CatchUpViewModel)viewModel.NavigationItems.Single(i => i.Label == "Rattrapage").Screen;
        var targetDate = catchUp.Days[0].Date;

        catchUp.Days[0].OpenCommand.Execute(null);

        var day = Assert.IsType<DayViewModel>(viewModel.CurrentScreen);
        Assert.Equal(targetDate, day.SelectedDate);
        Assert.True(viewModel.NavigationItems[0].IsSelected);
    }

    // La remontée du Rattrapage s'arrête au premier jour ouvré sans aucune ligne (cf. CatchUpDayCalculator) :
    // il faut une ligne sur le dernier jour ouvré avant "aujourd'hui" pour obtenir un jour non complété.
    private static FakeTimeBlockRepository RepositoryWithOneIncompleteDayBeforeToday()
    {
        var repository = new FakeTimeBlockRepository();
        var date = DateOnly.FromDateTime(DateTime.Today).AddDays(-1);
        while (!CatchUpDayCalculator.IsBusinessDay(date))
        {
            date = date.AddDays(-1);
        }

        repository.Insert(new TimeBlock(date, date.ToDateTime(TimeOnly.MinValue), date.ToDateTime(TimeOnly.MinValue).AddHours(1),
            "Résumé", "ULISTROIS-1", "POSID", "ZWPID", string.Empty, 1.0, TimeBlockStatus.Proposed, null));
        return repository;
    }

    private sealed class FakeSettingsRepository : ISettingsRepository
    {
        private readonly Dictionary<string, string> _values = [];

        public string? Get(string key) => _values.GetValueOrDefault(key);

        public void Set(string key, string value) => _values[key] = value;
    }
}
