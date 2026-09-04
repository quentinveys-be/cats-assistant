using System.Windows.Threading;
using CatsAssistant.App.Mvvm;
using CatsAssistant.Filler;

namespace CatsAssistant.App.ViewModels;

/// <summary>
/// Carte "Connexions" SAP (issue #24, logon réel issue #27). "Se connecter" délègue à
/// <see cref="ISapSessionProvider"/>, qui ouvre la fenêtre WebView2 (D4) ; aucun identifiant SAP n'est jamais
/// lu ni stocké ici. La pastille de statut suit <see cref="ISapSessionProvider.State"/>, y compris l'expiration
/// signalée par le client OData (401/302), pour proposer la reconnexion.
/// </summary>
public sealed class SapConnectionCardViewModel : ObservableObject, IDisposable
{
    private readonly ISapSessionProvider _sessionProvider;
    private readonly Dispatcher _dispatcher;
    private bool _isConnecting;
    private string? _errorMessage;

    public SapConnectionCardViewModel(ISapSessionProvider sessionProvider)
    {
        _sessionProvider = sessionProvider;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _sessionProvider.StateChanged += OnSessionStateChanged;

        OpenLogonCommand = new RelayCommand(() => _ = ConnectAsync(), () => !IsConnecting);
    }

    public RelayCommand OpenLogonCommand { get; }

    public ConnectionStatus Status => _sessionProvider.State switch
    {
        SapSessionState.Connected => ConnectionStatus.Connected,
        SapSessionState.Expired => ConnectionStatus.Expired,
        _ => ConnectionStatus.NotConfigured,
    };

    public string StatusLabel => Status switch
    {
        ConnectionStatus.Connected => "connecté",
        ConnectionStatus.Expired => "session expirée — reconnexion nécessaire",
        _ => "non configuré",
    };

    public bool IsConnecting
    {
        get => _isConnecting;
        private set => SetProperty(ref _isConnecting, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public async Task ConnectAsync()
    {
        if (IsConnecting)
        {
            return;
        }

        IsConnecting = true;
        ErrorMessage = null;

        var connected = await _sessionProvider.EnsureLogonAsync();

        IsConnecting = false;
        ErrorMessage = connected ? null : "Connexion annulée : la fenêtre a été fermée avant la fin du logon.";
        RefreshStatus();
    }

    private void OnSessionStateChanged(object? sender, EventArgs e) => _dispatcher.BeginInvoke(RefreshStatus);

    private void RefreshStatus()
    {
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusLabel));
    }

    public void Dispose() => _sessionProvider.StateChanged -= OnSessionStateChanged;
}
