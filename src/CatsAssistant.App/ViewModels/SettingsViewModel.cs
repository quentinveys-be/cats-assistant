using CatsAssistant.App.Mvvm;

namespace CatsAssistant.App.ViewModels;

public enum SettingsTab
{
    Connexions,
    Regles,
    Capture,
    Donnees,
}

/// <summary>
/// Écran "Paramètres" : onglets Connexions et Règles (issue #24) + Capture et Données (issue #23).
/// <see cref="Connections"/> et <see cref="Rules"/> sont null quand leurs dépendances (coffre, base
/// métier) ne sont pas disponibles (coffre verrouillé au démarrage) — la vue affiche alors un message
/// dégradé plutôt que de planter. Capture et Données ne dépendent que de la base d'activité, toujours
/// ouverte, donc jamais null.
/// </summary>
public sealed class SettingsViewModel : ScreenViewModelBase, IDisposable
{
    private SettingsTab _selectedTab = SettingsTab.Connexions;

    public SettingsViewModel(
        ConnectionsViewModel? connections = null,
        RulesViewModel? rules = null,
        CaptureSettingsViewModel? capture = null,
        DataSettingsViewModel? data = null)
        : base("Paramètres")
    {
        Connections = connections;
        Rules = rules;
        Capture = capture ?? new CaptureSettingsViewModel();
        Data = data ?? new DataSettingsViewModel();

        SelectConnexionsCommand = new RelayCommand(() => SelectedTab = SettingsTab.Connexions);
        SelectReglesCommand = new RelayCommand(() => SelectedTab = SettingsTab.Regles);
        SelectCaptureTabCommand = new RelayCommand(() => SelectedTab = SettingsTab.Capture);
        SelectDataTabCommand = new RelayCommand(() => SelectedTab = SettingsTab.Donnees);
    }

    public ConnectionsViewModel? Connections { get; }

    public RulesViewModel? Rules { get; }

    public CaptureSettingsViewModel Capture { get; }

    public DataSettingsViewModel Data { get; }

    public SettingsTab SelectedTab
    {
        get => _selectedTab;
        private set
        {
            if (SetProperty(ref _selectedTab, value))
            {
                OnPropertyChanged(nameof(IsCaptureTabSelected));
                OnPropertyChanged(nameof(IsDataTabSelected));
            }
        }
    }

    // Bindings de l'onglet Capture/Données (issue #23), conservés tels quels sur le sélecteur commun.
    public bool IsCaptureTabSelected => SelectedTab == SettingsTab.Capture;

    public bool IsDataTabSelected => SelectedTab == SettingsTab.Donnees;

    public RelayCommand SelectConnexionsCommand { get; }

    public RelayCommand SelectReglesCommand { get; }

    public RelayCommand SelectCaptureTabCommand { get; }

    public RelayCommand SelectDataTabCommand { get; }

    public void Dispose() => Connections?.Dispose();
}
