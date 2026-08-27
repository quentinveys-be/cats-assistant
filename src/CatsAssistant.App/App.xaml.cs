using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Forms;
using CatsAssistant.App.Themes;
using CatsAssistant.App.ViewModels;
using CatsAssistant.App.Views;
using CatsAssistant.Collector;
using CatsAssistant.Connectors;
using CatsAssistant.Secrets;
using CatsAssistant.Store;
using Microsoft.Data.Sqlite;
using Application = System.Windows.Application;

namespace CatsAssistant.App;

/// <summary>
/// Interaction logic for App.xaml — starts in the system tray, no main window by default.
/// The Collector runs in-process here rather than as a separate service, since the project is user-mode only.
/// </summary>
public partial class App : Application
{
    private readonly StartupRegistration _startupRegistration = new();

    private SqliteConnection? _connection;
    private SqliteConnection? _businessConnection;
    private bool _businessVaultLocked;
    private IActivityEventRepository? _repository;
    private ISettingsRepository? _settingsRepository;
    private ActivityCollector? _collector;
    private SyncService? _syncService;
    private HttpClient? _jiraHttpClient;
    private HttpClient? _gitLabHttpClient;
    private NotifyIcon? _trayIcon;
    private ToolStripMenuItem? _toggleCaptureItem;
    private ToolStripMenuItem? _syncNowItem;
    private MainWindow? _mainWindow;
    private string _dataDirectory = string.Empty;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var legacyDatabasePath = SqliteConnectionFactory.GetDefaultDatabasePath();
        var activityDatabasePath = SqliteConnectionFactory.GetDefaultActivityDatabasePath();
        _dataDirectory = Path.GetDirectoryName(activityDatabasePath)!;

        var activityKey = new DpapiActivityKeyStore(DpapiActivityKeyStore.GetDefaultKeyFilePath()).GetOrCreateKey();
        new ActivityDatabaseMigration(legacyDatabasePath, activityDatabasePath, activityKey).Run();

        var connectionFactory = new SqliteConnectionFactory(activityDatabasePath, activityKey);
        _connection = connectionFactory.OpenConnection();

        _repository = new SqliteActivityEventRepository(_connection);
        _settingsRepository = new SqliteSettingsRepository(_connection);
        ThemeService.Apply(_settingsRepository.Get("ui.theme") == "dark");

        // Retention is 90 days (docs/data-model.md, ADR D3); startup is the only moment the app is
        // guaranteed to reach in a user-mode, no-scheduler deployment.
        new ActivityEventRetentionPurger(_repository).Purge();

        _collector = new ActivityCollector(_repository);
        _collector.Start();

        OpenBusinessDatabase();
        InitializeSyncService();

        _trayIcon = BuildTrayIcon();
    }

    // Composition root des 3 connecteurs (docs/phases.md, étape 2.6) : chacun est optionnel, un
    // connecteur non configuré (variable d'environnement absente) ou un coffre métier verrouillé
    // dégrade la synchro en mode "non configuré" plutôt que d'empêcher le démarrage de l'app.
    private void InitializeSyncService()
    {
        if (_businessConnection is null)
        {
            return;
        }

        var vault = new DpapiYubiKeySecretVault(
            DpapiYubiKeySecretVault.GetDefaultVaultDirectory(),
            new YubiKeyChallengeResponseClient());

        var jiraConnector = BuildJiraConnector(vault);
        var gitLabConnector = BuildGitLabConnector(vault, out var gitLabTargets);
        var outlookConnector = new OutlookComConnector();

        _syncService = new SyncService(
            jiraConnector,
            gitLabConnector,
            outlookConnector,
            new SqliteJiraTicketRepository(_businessConnection),
            new SqliteVcsCommitRepository(_businessConnection),
            new SqliteCalendarEventRepository(_businessConnection),
            gitLabTargets: gitLabTargets);

        var intervalMinutes = Environment.GetEnvironmentVariable("CATS_SYNC_INTERVAL_MINUTES");
        if (int.TryParse(intervalMinutes, out var minutes) && minutes > 0)
        {
            _syncService.StartPeriodicSync(TimeSpan.FromMinutes(minutes));
        }
    }

    // Instance JIRA fixée par l'ADR D7 (ulis-uliege.atlassian.net) ; seul l'e-mail du compte (non
    // secret) reste à fournir — pas encore de config utilisateur (onboarding, Phase 5), donc lu depuis
    // une variable d'environnement en attendant.
    private IJiraConnector? BuildJiraConnector(DpapiYubiKeySecretVault vault)
    {
        var accountEmail = Environment.GetEnvironmentVariable("CATS_JIRA_ACCOUNT_EMAIL");
        if (string.IsNullOrWhiteSpace(accountEmail))
        {
            return null;
        }

        var options = new JiraConnectorOptions(new Uri("https://ulis-uliege.atlassian.net/"), accountEmail);
        _jiraHttpClient = new HttpClient();
        return new JiraCloudConnector(_jiraHttpClient, new VaultJiraTokenProvider(vault), options);
    }

    // Pas de découverte GitLab (l'API ne liste pas "mes dépôts" de façon exploitable ici) : la base
    // URL et la liste projet:branche sont lues depuis l'environnement en attendant l'onboarding config
    // (Phase 5, docs/phases.md).
    private IGitLabConnector? BuildGitLabConnector(DpapiYubiKeySecretVault vault, out IReadOnlyList<GitLabSyncTarget> targets)
    {
        targets = [];

        var baseUrlRaw = Environment.GetEnvironmentVariable("CATS_GITLAB_BASE_URL");
        if (string.IsNullOrWhiteSpace(baseUrlRaw) || !Uri.TryCreate(baseUrlRaw, UriKind.Absolute, out var baseUrl))
        {
            return null;
        }

        targets = ParseGitLabTargets(Environment.GetEnvironmentVariable("CATS_GITLAB_PROJECTS"));
        if (targets.Count == 0)
        {
            return null;
        }

        _gitLabHttpClient = new HttpClient { BaseAddress = baseUrl };
        return new GitLabConnector(_gitLabHttpClient, new VaultGitLabTokenProvider(vault));
    }

    // Format : "projectId:branch,projectId2:branch2" ; toute entrée mal formée est ignorée plutôt que
    // de faire échouer le démarrage.
    private static IReadOnlyList<GitLabSyncTarget> ParseGitLabTargets(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        var targets = new List<GitLabSyncTarget>();
        foreach (var entry in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = entry.Split(':', 2);
            if (parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0)
            {
                targets.Add(new GitLabSyncTarget(parts[0], parts[1]));
            }
        }

        return targets;
    }

    // Mode dégradé (docs/adr/D6) : sans YubiKey (absente ou refusée), la base métier reste fermée mais la
    // capture d'activité (activity.db, DPAPI) continue normalement — jamais de crash ni de blocage.
    private void OpenBusinessDatabase()
    {
        var keyProvider = new BusinessMasterKeyProvider(
            BusinessMasterKeyProvider.GetDefaultChallengeFilePath(),
            new YubiKeyChallengeResponseClient());
        var businessKey = keyProvider.TryGetOrDeriveKey();

        if (businessKey is null)
        {
            _businessVaultLocked = true;
            return;
        }

        try
        {
            var businessDatabasePath = SqliteConnectionFactory.GetDefaultBusinessDatabasePath();
            var businessMigrator = new SqliteMigrator(SqliteMigrator.BusinessMigrations);
            _businessConnection = new SqliteConnectionFactory(businessDatabasePath, businessKey, businessMigrator).OpenConnection();
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
        {
            // Clé dérivable mais base illisible (challenge régénéré/perdu après création, fichier corrompu) :
            // mode dégradé plutôt que crash au démarrage (docs/adr/D6).
            _businessVaultLocked = true;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _collector?.Stop();
        _collector?.Dispose();

        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }

        _syncService?.Dispose();
        _jiraHttpClient?.Dispose();
        _gitLabHttpClient?.Dispose();

        _connection?.Dispose();
        _businessConnection?.Dispose();

        base.OnExit(e);
    }

    private NotifyIcon BuildTrayIcon()
    {
        _toggleCaptureItem = new ToolStripMenuItem(ToggleCaptureLabel(), null, OnToggleCapture);

        var openDataFolderItem = new ToolStripMenuItem("Ouvrir le dossier de données", null, OnOpenDataFolder);

        var openMainWindowItem = new ToolStripMenuItem("Ouvrir CATS Assistant", null, OnOpenMainWindow);

        _syncNowItem = new ToolStripMenuItem("Synchroniser maintenant", null, OnSyncNow)
        {
            Enabled = _syncService is not null,
        };

        var startWithWindowsItem = new ToolStripMenuItem("Démarrer avec Windows")
        {
            CheckOnClick = true,
            Checked = _startupRegistration.IsEnabled(),
        };
        startWithWindowsItem.Click += OnToggleStartWithWindows;

        var exitItem = new ToolStripMenuItem("Quitter", null, OnExitClicked);

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(_toggleCaptureItem);
        contextMenu.Items.Add(openDataFolderItem);
        contextMenu.Items.Add(openMainWindowItem);
        contextMenu.Items.Add(_syncNowItem);
        contextMenu.Items.Add(startWithWindowsItem);

        if (_businessVaultLocked)
        {
            contextMenu.Items.Add(new ToolStripMenuItem("Coffre métier verrouillé") { Enabled = false });
        }

        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        return new NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "CATS Assistant",
            Visible = true,
            ContextMenuStrip = contextMenu,
        };
    }

    private string ToggleCaptureLabel() =>
        _collector is { IsRunning: true } ? "Mettre en pause la capture" : "Démarrer la capture";

    private void OnToggleCapture(object? sender, EventArgs e)
    {
        if (_collector is null) return;

        if (_collector.IsRunning)
        {
            _collector.Stop();
        }
        else
        {
            _collector.Start();
        }

        if (_toggleCaptureItem is not null)
        {
            _toggleCaptureItem.Text = ToggleCaptureLabel();
        }
    }

    private void OnOpenDataFolder(object? sender, EventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = _dataDirectory,
            UseShellExecute = true,
        });
    }

    private void OnOpenMainWindow(object? sender, EventArgs e)
    {
        if (_mainWindow is not null)
        {
            _mainWindow.Activate();
            return;
        }

        _mainWindow = new MainWindow(new MainWindowViewModel(_syncService, _settingsRepository));
        _mainWindow.Closed += (_, _) => _mainWindow = null;
        _mainWindow.Show();
    }

    private async void OnSyncNow(object? sender, EventArgs e)
    {
        if (_syncService is null) return;

        await _syncService.SyncAllAsync();
    }

    private void OnToggleStartWithWindows(object? sender, EventArgs e)
    {
        if (sender is not ToolStripMenuItem item) return;

        if (item.Checked)
        {
            _startupRegistration.Enable(Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule!.FileName);
        }
        else
        {
            _startupRegistration.Disable();
        }
    }

    private void OnExitClicked(object? sender, EventArgs e)
    {
        Shutdown();
    }
}
