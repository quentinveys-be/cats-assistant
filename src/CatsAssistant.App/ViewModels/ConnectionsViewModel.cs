using System.Windows.Threading;
using CatsAssistant.App.Mvvm;
using CatsAssistant.Secrets;
using CatsAssistant.Store;

namespace CatsAssistant.App.ViewModels;

/// <summary>Onglet "Connexions" des Paramètres (issue #24) : agrège les 5 cartes de connecteur.</summary>
public sealed class ConnectionsViewModel : ObservableObject, IDisposable
{
    private readonly SyncService? _syncService;
    private readonly Dispatcher _dispatcher;

    public ConnectionsViewModel(
        ISecretVault vault,
        ISettingsRepository settingsRepository,
        YubiKeyVaultCoordinator vaultCoordinator,
        SyncService? syncService)
    {
        _syncService = syncService;
        _dispatcher = Dispatcher.CurrentDispatcher;

        Sap = new SapConnectionCardViewModel();
        Jira = new TokenConnectionCardViewModel(
            "JIRA Cloud — ulis-uliege.atlassian.net",
            "Token API personnel · coffre YubiKey",
            vault, settingsRepository, SecretName.JiraApiToken, "secrets.jira", tracksExpiry: false);
        // ponytail : expiration saisie manuellement (le connecteur GitLab n'expose pas encore l'endpoint
        // personal_access_tokens/self) plutôt que récupérée automatiquement ; à automatiser si IGitLabConnector
        // gagne un jour cette capacité.
        GitLab = new TokenConnectionCardViewModel(
            "GitLab — gitlab.uliege.be",
            "Token personnel",
            vault, settingsRepository, SecretName.GitLabPersonalToken, "secrets.gitlab", tracksExpiry: true);
        YubiKey = new YubiKeyConnectionCardViewModel(vaultCoordinator);
        Outlook = new OutlookConnectionCardViewModel();

        RefreshSyncDetails();

        if (_syncService is not null)
        {
            _syncService.StateChanged += OnSyncStateChanged;
        }
    }

    public SapConnectionCardViewModel Sap { get; }

    public TokenConnectionCardViewModel Jira { get; }

    public TokenConnectionCardViewModel GitLab { get; }

    public YubiKeyConnectionCardViewModel YubiKey { get; }

    public OutlookConnectionCardViewModel Outlook { get; }

    private void OnSyncStateChanged(object? sender, EventArgs e) => _dispatcher.BeginInvoke(RefreshSyncDetails);

    private void RefreshSyncDetails()
    {
        if (_syncService is null)
        {
            return;
        }

        Jira.LastSyncText = FormatLastSync(_syncService.GetState(SyncConnector.Jira));
        GitLab.LastSyncText = FormatLastSync(_syncService.GetState(SyncConnector.GitLab));
        Outlook.Update(_syncService.GetState(SyncConnector.Outlook));
    }

    private static string? FormatLastSync(SyncConnectorState state) =>
        state.LastSyncUtc is { } lastSync ? $"dernière synchro {lastSync.ToLocalTime():HH:mm}" : null;

    public void Dispose()
    {
        if (_syncService is not null)
        {
            _syncService.StateChanged -= OnSyncStateChanged;
        }
    }
}
