using System.Collections.ObjectModel;
using CatsAssistant.App.Mvvm;
using CatsAssistant.Store;

namespace CatsAssistant.App.ViewModels;

/// <summary>
/// VM de la fenêtre principale (issue #15) : rail de navigation entre les 4 écrans + barre d'état.
/// Le contenu de chaque écran est hors périmètre de ce shell.
/// </summary>
public sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private const string ThemeSettingKey = "ui.theme";
    private const string DarkThemeSettingValue = "dark";

    private readonly ISettingsRepository? _settingsRepository;
    private ScreenViewModelBase _currentScreen;
    private bool _isDarkTheme;

    public MainWindowViewModel(SyncService? syncService, ISettingsRepository? settingsRepository = null)
    {
        _settingsRepository = settingsRepository;

        var day = new NavigationItemViewModel("Journée", new DayViewModel(), Select);
        var catchUp = new NavigationItemViewModel("Rattrapage", new CatchUpViewModel(), Select);
        var summary = new NavigationItemViewModel("Récapitulatif", new SummaryViewModel(), Select);
        SettingsItem = new NavigationItemViewModel("Paramètres", new SettingsViewModel(), Select);

        NavigationItems = [day, catchUp, summary];

        StatusBar = new StatusBarViewModel(syncService);

        _isDarkTheme = _settingsRepository?.Get(ThemeSettingKey) == DarkThemeSettingValue;
        ToggleThemeCommand = new RelayCommand(() =>
        {
            IsDarkTheme = !IsDarkTheme;
            _settingsRepository?.Set(ThemeSettingKey, IsDarkTheme ? DarkThemeSettingValue : "light");
        });

        _currentScreen = day.Screen;
        day.IsSelected = true;
    }

    public IReadOnlyList<NavigationItemViewModel> NavigationItems { get; }

    public NavigationItemViewModel SettingsItem { get; }

    public StatusBarViewModel StatusBar { get; }

    public RelayCommand ToggleThemeCommand { get; }

    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        private set => SetProperty(ref _isDarkTheme, value);
    }

    public ScreenViewModelBase CurrentScreen
    {
        get => _currentScreen;
        private set => SetProperty(ref _currentScreen, value);
    }

    private void Select(NavigationItemViewModel item)
    {
        foreach (var navigationItem in NavigationItems)
        {
            navigationItem.IsSelected = navigationItem == item;
        }

        SettingsItem.IsSelected = SettingsItem == item;
        CurrentScreen = item.Screen;
    }

    public void Dispose() => StatusBar.Dispose();
}
