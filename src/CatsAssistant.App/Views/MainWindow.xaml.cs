using System.ComponentModel;
using System.Windows;
using CatsAssistant.App.Themes;
using CatsAssistant.App.ViewModels;

namespace CatsAssistant.App.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        StateChanged += OnStateChanged;
        Closed += (_, _) => _viewModel.Dispose();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    // La fenêtre reste seule consommatrice du service de thème WPF (ResourceDictionary) : le VM ne
    // touche jamais Application.Current, ce qui le garde testable hors contexte WPF.
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsDarkTheme))
        {
            ThemeService.Apply(_viewModel.IsDarkTheme);
        }
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeRestoreClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    // WindowChrome ajoute ~7px de débordement hors écran en plein écran (bug connu WPF) — on gonfle la
    // bordure de la racine pour compenser au lieu de laisser le contenu déborder sous la barre des tâches.
    private void OnStateChanged(object? sender, EventArgs e) =>
        RootBorder.BorderThickness = new Thickness(WindowState == WindowState.Maximized ? 7 : 1);
}
