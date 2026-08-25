using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using CatsAssistant.Collector;
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
    private ActivityCollector? _collector;
    private NotifyIcon? _trayIcon;
    private ToolStripMenuItem? _toggleCaptureItem;
    private TodayEventsWindow? _todayEventsWindow;
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

        // Retention is 90 days (docs/data-model.md, ADR D3); startup is the only moment the app is
        // guaranteed to reach in a user-mode, no-scheduler deployment.
        new ActivityEventRetentionPurger(_repository).Purge();

        _collector = new ActivityCollector(_repository);
        _collector.Start();

        OpenBusinessDatabase();

        _trayIcon = BuildTrayIcon();
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

        _connection?.Dispose();
        _businessConnection?.Dispose();

        base.OnExit(e);
    }

    private NotifyIcon BuildTrayIcon()
    {
        _toggleCaptureItem = new ToolStripMenuItem(ToggleCaptureLabel(), null, OnToggleCapture);

        var openDataFolderItem = new ToolStripMenuItem("Ouvrir le dossier de données", null, OnOpenDataFolder);

        var showTodayEventsItem = new ToolStripMenuItem("Afficher les événements du jour", null, OnShowTodayEvents);

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
        contextMenu.Items.Add(showTodayEventsItem);
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

    private void OnShowTodayEvents(object? sender, EventArgs e)
    {
        if (_repository is null) return;

        if (_todayEventsWindow is not null)
        {
            _todayEventsWindow.Activate();
            return;
        }

        _todayEventsWindow = new TodayEventsWindow(_repository);
        _todayEventsWindow.Closed += (_, _) => _todayEventsWindow = null;
        _todayEventsWindow.Show();
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
