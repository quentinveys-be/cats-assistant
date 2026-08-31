using CatsAssistant.App.Mvvm;

namespace CatsAssistant.App.ViewModels;

/// <summary>Écran Paramètres (issue #23) : onglets Capture et Données. Connexions et Règles sont hors
/// périmètre (issues dédiées).</summary>
public sealed class SettingsViewModel : ScreenViewModelBase
{
    private bool _isDataTabSelected;

    public SettingsViewModel(CaptureSettingsViewModel? capture = null, DataSettingsViewModel? data = null)
        : base("Paramètres")
    {
        Capture = capture ?? new CaptureSettingsViewModel();
        Data = data ?? new DataSettingsViewModel();

        SelectCaptureTabCommand = new RelayCommand(() => IsDataTabSelected = false);
        SelectDataTabCommand = new RelayCommand(() => IsDataTabSelected = true);
    }

    public CaptureSettingsViewModel Capture { get; }

    public DataSettingsViewModel Data { get; }

    public RelayCommand SelectCaptureTabCommand { get; }

    public RelayCommand SelectDataTabCommand { get; }

    public bool IsCaptureTabSelected => !_isDataTabSelected;

    public bool IsDataTabSelected
    {
        get => _isDataTabSelected;
        private set
        {
            if (SetProperty(ref _isDataTabSelected, value))
            {
                OnPropertyChanged(nameof(IsCaptureTabSelected));
            }
        }
    }
}
