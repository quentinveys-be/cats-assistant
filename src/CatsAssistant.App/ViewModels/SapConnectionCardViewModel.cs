using CatsAssistant.App.Mvvm;

namespace CatsAssistant.App.ViewModels;

/// <summary>
/// Carte "Connexions" SAP (issue #24). Le logon SAP réel (fenêtre WebView2, D4) est Phase 4 : hors périmètre
/// de cette issue. "Se connecter" reste affiché mais ne fait qu'annoncer la disponibilité future, jamais
/// de crédential SAP stocké ni simulé ici (docs/adr/D4-sap-odata-webview2.md).
/// </summary>
public sealed class SapConnectionCardViewModel : ObservableObject
{
    private bool _showStubMessage;

    public SapConnectionCardViewModel()
    {
        OpenLogonCommand = new RelayCommand(() => ShowStubMessage = true);
    }

    public RelayCommand OpenLogonCommand { get; }

    public ConnectionStatus Status => ConnectionStatus.NotConfigured;

    public string StatusLabel => "non configuré";

    public bool ShowStubMessage
    {
        get => _showStubMessage;
        private set => SetProperty(ref _showStubMessage, value);
    }
}
