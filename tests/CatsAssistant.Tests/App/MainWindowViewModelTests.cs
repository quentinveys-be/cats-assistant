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

    private sealed class FakeSettingsRepository : ISettingsRepository
    {
        private readonly Dictionary<string, string> _values = [];

        public string? Get(string key) => _values.GetValueOrDefault(key);

        public void Set(string key, string value) => _values[key] = value;
    }
}
