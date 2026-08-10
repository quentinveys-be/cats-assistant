using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using CatsAssistant.Collector;
using CatsAssistant.Store;
using Microsoft.Data.Sqlite;
using Application = System.Windows.Application;

namespace CatsAssistant.App;

/// <summary>
/// Interaction logic for App.xaml — starts in the system tray, no main window by default.
/// The Collector (step-1.5) runs in-process here, not as a separate service (CONVENTIONS.md decision #6).
/// </summary>
public partial class App : Application
{
    private readonly StartupRegistration _startupRegistration = new();

    private SqliteConnection? _connection;
    private ActivityCollector? _collector;
    private NotifyIcon? _trayIcon;
    private ToolStripMenuItem? _toggleCaptureItem;
    private string _dataDirectory = string.Empty;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var databasePath = SqliteConnectionFactory.GetDefaultDatabasePath();
        _dataDirectory = Path.GetDirectoryName(databasePath)!;

        var connectionFactory = new SqliteConnectionFactory(databasePath);
        _connection = connectionFactory.OpenConnection();

        var repository = new SqliteActivityEventRepository(_connection);
        _collector = new ActivityCollector(repository);
        _collector.Start();

        _trayIcon = BuildTrayIcon();
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

        base.OnExit(e);
    }

    private NotifyIcon BuildTrayIcon()
    {
        _toggleCaptureItem = new ToolStripMenuItem(ToggleCaptureLabel(), null, OnToggleCapture);

        var openDataFolderItem = new ToolStripMenuItem("Ouvrir le dossier de données", null, OnOpenDataFolder);

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
        contextMenu.Items.Add(startWithWindowsItem);
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
