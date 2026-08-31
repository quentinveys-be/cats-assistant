using CatsAssistant.App.Mvvm;

namespace CatsAssistant.App.ViewModels;

public enum SettingsTab
{
    Connexions,
    Regles,
}

/// <summary>
/// Écran "Paramètres" (issue #24) : onglets Connexions et Règles. <see cref="Connections"/> et
/// <see cref="Rules"/> sont null quand leurs dépendances (coffre, base métier) ne sont pas disponibles
/// (coffre verrouillé au démarrage) — la vue affiche alors un message dégradé plutôt que de planter.
/// </summary>
public sealed class SettingsViewModel : ScreenViewModelBase, IDisposable
{
    private SettingsTab _selectedTab = SettingsTab.Connexions;

    public SettingsViewModel(ConnectionsViewModel? connections = null, RulesViewModel? rules = null)
        : base("Paramètres")
    {
        Connections = connections;
        Rules = rules;

        SelectConnexionsCommand = new RelayCommand(() => SelectedTab = SettingsTab.Connexions);
        SelectReglesCommand = new RelayCommand(() => SelectedTab = SettingsTab.Regles);
    }

    public ConnectionsViewModel? Connections { get; }

    public RulesViewModel? Rules { get; }

    public SettingsTab SelectedTab
    {
        get => _selectedTab;
        private set => SetProperty(ref _selectedTab, value);
    }

    public RelayCommand SelectConnexionsCommand { get; }

    public RelayCommand SelectReglesCommand { get; }

    public void Dispose() => Connections?.Dispose();
}
