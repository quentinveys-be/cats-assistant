using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Windows;
using System.Windows.Forms;
using CatsAssistant.App.Services;
using CatsAssistant.App.Themes;
using CatsAssistant.App.ViewModels;
using CatsAssistant.App.Views;
using CatsAssistant.Collector;
using CatsAssistant.Connectors;
using CatsAssistant.Secrets;
using CatsAssistant.Store;
using Microsoft.Data.Sqlite;
using Application = System.Windows.Application;
using Icon = System.Drawing.Icon;

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
    private IActivityEventRepository? _repository;
    private ISettingsRepository? _settingsRepository;
    private ITimeBlockRepository? _timeBlockRepository;
    private ActivityCollector? _collector;
    private SyncService? _syncService;
    private YubiKeyVaultCoordinator? _vaultCoordinator;
    private ISecretVault? _secretVault;
    private IRuleRepository? _ruleRepository;
    private bool _yubiKeyDialogOpen;
    private HttpClient? _jiraHttpClient;
    private HttpClient? _gitLabHttpClient;
    private NotifyIcon? _trayIcon;
    private ToolStripMenuItem? _headerItem;
    private ToolStripMenuItem? _toggleCaptureItem;
    private ToolStripMenuItem? _catchUpItem;
    private ToolStripMenuItem? _syncNowItem;
    private Bitmap? _activeDotImage;
    private Bitmap? _pausedDotImage;
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

        _vaultCoordinator = new YubiKeyVaultCoordinator(new BusinessMasterKeyProvider(
            BusinessMasterKeyProvider.GetDefaultChallengeFilePath(),
            new YubiKeyChallengeResponseClient()));

        // Coffre de tokens JIRA/GitLab (ADR D6) : indépendant de business.db, donc construit ici
        // inconditionnellement pour rester utilisable (carte Connexions) même coffre métier verrouillé.
        _secretVault = new DpapiYubiKeySecretVault(
            DpapiYubiKeySecretVault.GetDefaultVaultDirectory(),
            new YubiKeyChallengeResponseClient());

        // Pas de dialogue si aucune YubiKey n'est branchée (rien à toucher) : dégrade en silence plutôt
        // que d'inviter à un geste impossible (issue #26, "sans double invite").
        if (_vaultCoordinator.IsYubiKeyPresent)
        {
            ShowYubiKeyUnlockDialog();
        }
        else
        {
            _vaultCoordinator.ContinueWithoutVault();
        }

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

        var jiraConnector = BuildJiraConnector(_secretVault!);
        var gitLabConnector = BuildGitLabConnector(_secretVault!, out var gitLabTargets);
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
    private IJiraConnector? BuildJiraConnector(ISecretVault vault)
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
    private IGitLabConnector? BuildGitLabConnector(ISecretVault vault, out IReadOnlyList<GitLabSyncTarget> targets)
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

    // Mode dégradé (docs/adr/D6) : sans YubiKey (absente, refusée ou "Continuer sans coffre", issue #26),
    // la base métier reste fermée mais la capture d'activité (activity.db, DPAPI) continue normalement —
    // jamais de crash ni de blocage. Réutilise la clé déjà dérivée par _vaultCoordinator (un seul appui
    // YubiKey par session) plutôt que d'en redériver une ici.
    private void OpenBusinessDatabase()
    {
        if (_vaultCoordinator!.State != YubiKeyVaultState.Unlocked)
        {
            return;
        }

        try
        {
            var businessDatabasePath = SqliteConnectionFactory.GetDefaultBusinessDatabasePath();
            var businessMigrator = new SqliteMigrator(SqliteMigrator.BusinessMigrations);
            _businessConnection = new SqliteConnectionFactory(businessDatabasePath, _vaultCoordinator.CachedKey!, businessMigrator).OpenConnection();
            _timeBlockRepository = new SqliteTimeBlockRepository(_businessConnection);
            _ruleRepository = new SqliteRuleRepository(_businessConnection);
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
        {
            // Clé dérivée mais base illisible (challenge régénéré/perdu après création, fichier corrompu) :
            // mode dégradé plutôt que crash au démarrage (docs/adr/D6).
        }
    }

    // Un seul dialogue à la fois (issue #26, "sans double invite") : démarrage, "Tester la clé" (tray) et
    // "Synchroniser maintenant" coffre verrouillé peuvent tous trois demander une invite.
    private void ShowYubiKeyUnlockDialog()
    {
        if (_yubiKeyDialogOpen)
        {
            return;
        }

        _yubiKeyDialogOpen = true;
        try
        {
            var dialog = new YubiKeyUnlockDialog(new YubiKeyUnlockViewModel(_vaultCoordinator!)) { Owner = _mainWindow };
            dialog.ShowDialog();
        }
        finally
        {
            _yubiKeyDialogOpen = false;
        }
    }

    // Point d'entrée des déclenchements à la demande (issue #26) : tente le déverrouillage si nécessaire,
    // puis (ré)ouvre business.db et la synchro si ce n'était pas déjà fait.
    private void UnlockVaultOnDemandAndInitializeSync()
    {
        if (_businessConnection is not null)
        {
            return;
        }

        ShowYubiKeyUnlockDialog();

        if (_vaultCoordinator!.State == YubiKeyVaultState.Unlocked)
        {
            OpenBusinessDatabase();
            InitializeSyncService();
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

        _activeDotImage?.Dispose();
        _pausedDotImage?.Dispose();

        _syncService?.Dispose();
        _jiraHttpClient?.Dispose();
        _gitLabHttpClient?.Dispose();

        _connection?.Dispose();
        _businessConnection?.Dispose();

        base.OnExit(e);
    }

    // Design du menu tray (issue #25, docs/design/screens/cats-assistant.dc.html — overlay "tray") : en-tête
    // d'état, actions principales, Paramètres, puis les actions historiques (dossier de données, synchro
    // manuelle, démarrage Windows) qui n'ont pas encore d'écran dédié où vivre.
    private NotifyIcon BuildTrayIcon()
    {
        _activeDotImage = CreateDotImage(ColorTranslator.FromHtml("#0F7B0F"));
        _pausedDotImage = CreateDotImage(ColorTranslator.FromHtml("#8A8A8A"));

        _headerItem = new ToolStripMenuItem { Enabled = false };

        var openDayItem = new ToolStripMenuItem("Ouvrir la journée", null,
            (_, _) => OpenMainWindow(SelectDay));

        _toggleCaptureItem = new ToolStripMenuItem(ToggleCaptureLabel(), null, OnToggleCapture);

        _catchUpItem = new ToolStripMenuItem("Rattrapage", null,
            (_, _) => OpenMainWindow(SelectCatchUp));

        var settingsItem = new ToolStripMenuItem("Paramètres", null,
            (_, _) => OpenMainWindow(SelectSettings));

        var openDataFolderItem = new ToolStripMenuItem("Ouvrir le dossier de données", null, OnOpenDataFolder);

        // Toujours activé (issue #26) : coffre verrouillé, un clic tente le déverrouillage avant de
        // synchroniser plutôt que de rester désactivé jusqu'au redémarrage.
        _syncNowItem = new ToolStripMenuItem("Synchroniser maintenant", null, OnSyncNow);

        var testYubiKeyItem = new ToolStripMenuItem("Tester la clé YubiKey", null, OnTestYubiKey);

        var startWithWindowsItem = new ToolStripMenuItem("Démarrer avec Windows")
        {
            CheckOnClick = true,
            Checked = _startupRegistration.IsEnabled(),
        };
        startWithWindowsItem.Click += OnToggleStartWithWindows;

        var exitItem = new ToolStripMenuItem("Quitter", null, OnExitClicked);

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(_headerItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(openDayItem);
        contextMenu.Items.Add(_toggleCaptureItem);
        contextMenu.Items.Add(_catchUpItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(settingsItem);
        contextMenu.Items.Add(openDataFolderItem);
        contextMenu.Items.Add(_syncNowItem);
        contextMenu.Items.Add(testYubiKeyItem);
        contextMenu.Items.Add(startWithWindowsItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitItem);
        contextMenu.Opening += (_, _) => RefreshTrayMenu();

        RefreshTrayMenu();

        var trayIcon = new NotifyIcon
        {
            Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule!.FileName)
                   ?? SystemIcons.Application,
            Text = "CATS Assistant",
            Visible = true,
            ContextMenuStrip = contextMenu,
        };
        trayIcon.MouseClick += OnTrayIconMouseClick;

        return trayIcon;
    }

    private static Bitmap CreateDotImage(Color color, int size = 8)
    {
        var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(color);
        g.FillEllipse(brush, 0, 0, size - 1, size - 1);
        return bitmap;
    }

    private void OnTrayIconMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            OpenMainWindow();
        }
    }

    private void RefreshTrayMenu()
    {
        if (_toggleCaptureItem is not null)
        {
            _toggleCaptureItem.Text = ToggleCaptureLabel();
        }

        if (_headerItem is not null)
        {
            var isRunning = _collector is { IsRunning: true };
            _headerItem.Text = $"{(isRunning ? "Capture active" : "Capture en pause")} — {FormatDuration(GetCapturedTodayDuration())}";
            _headerItem.Image = isRunning ? _activeDotImage : _pausedDotImage;
        }

        if (_catchUpItem is not null)
        {
            var catchUpCount = GetCatchUpCount();
            _catchUpItem.Text = catchUpCount > 0 ? $"Rattrapage ({catchUpCount})" : "Rattrapage";
        }
    }

    private TimeSpan GetCapturedTodayDuration()
    {
        if (_repository is null)
        {
            return TimeSpan.Zero;
        }

        var localNow = DateTime.Now;
        var events = _repository.GetByDateRange(localNow.Date.ToUniversalTime(), localNow.ToUniversalTime());
        return ActivityEventAggregator.Aggregate(events)
            .Aggregate(TimeSpan.Zero, (total, segment) => total + (segment.EndUtc - segment.StartUtc));
    }

    private static string FormatDuration(TimeSpan duration) => $"{(int)duration.TotalHours}:{duration.Minutes:D2}";

    // Nombre de jours ouvrés non complétés (issue #22) : recalculé à chaque ouverture du menu tray
    // (contextMenu.Opening → RefreshTrayMenu), donc toujours synchronisé sans état à invalider.
    private int GetCatchUpCount()
    {
        if (_timeBlockRepository is null)
        {
            return 0;
        }

        var expectedHoursPerDay = WorkScheduleSettings.ExpectedHoursPerDay(_settingsRepository);
        return CatchUpDayCalculator
            .ComputeIncompleteDays(_timeBlockRepository, DateOnly.FromDateTime(DateTime.Today), expectedHoursPerDay)
            .Count(d => d.Status is not (CatchUpDayStatus.Validated or CatchUpDayStatus.InProgress));
    }

    private static void SelectDay(MainWindowViewModel viewModel) => viewModel.NavigationItems[0].SelectCommand.Execute(null);

    private static void SelectCatchUp(MainWindowViewModel viewModel) =>
        viewModel.NavigationItems.Single(item => item.Label == "Rattrapage").SelectCommand.Execute(null);

    private static void SelectSettings(MainWindowViewModel viewModel) => viewModel.SettingsItem.SelectCommand.Execute(null);

    private string ToggleCaptureLabel() =>
        _collector is { IsRunning: true } ? "Mettre la capture en pause" : "Reprendre la capture";

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

        RefreshTrayMenu();
    }

    private void OnOpenDataFolder(object? sender, EventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = _dataDirectory,
            UseShellExecute = true,
        });
    }

    private void OpenMainWindow(Action<MainWindowViewModel>? select = null)
    {
        if (_mainWindow is null)
        {
            var viewModel = _businessConnection is null
                ? new MainWindowViewModel(
                    _syncService, _settingsRepository, _vaultCoordinator, _repository, _timeBlockRepository,
                    secretVault: _secretVault)
                : new MainWindowViewModel(
                    _syncService,
                    _settingsRepository,
                    _vaultCoordinator,
                    _repository,
                    _timeBlockRepository,
                    new SqliteCalendarEventRepository(_businessConnection),
                    new SqliteVcsCommitRepository(_businessConnection),
                    _ruleRepository,
                    secretVault: _secretVault,
                    jiraTicketRepository: new SqliteJiraTicketRepository(_businessConnection));
            _mainWindow = new MainWindow(viewModel);
            _mainWindow.Closed += (_, _) => _mainWindow = null;
            select?.Invoke(viewModel);
            _mainWindow.Show();
            return;
        }

        if (select is not null && _mainWindow.DataContext is MainWindowViewModel currentViewModel)
        {
            select(currentViewModel);
        }

        _mainWindow.Activate();
    }

    private async void OnSyncNow(object? sender, EventArgs e)
    {
        UnlockVaultOnDemandAndInitializeSync();

        if (_syncService is null) return;

        await _syncService.SyncAllAsync();
        RefreshTrayMenu();
    }

    // "Tester la clé" (issue #26) : toujours affiché, même coffre déjà déverrouillé (confirme qu'il reste
    // accessible ; instantané puisque la clé est en cache, aucun nouvel appui YubiKey).
    private void OnTestYubiKey(object? sender, EventArgs e)
    {
        ShowYubiKeyUnlockDialog();

        if (_vaultCoordinator!.State == YubiKeyVaultState.Unlocked && _businessConnection is null)
        {
            OpenBusinessDatabase();
            InitializeSyncService();
            RefreshTrayMenu();
        }
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
