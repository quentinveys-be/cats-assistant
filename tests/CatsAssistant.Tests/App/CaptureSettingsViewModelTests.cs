using CatsAssistant.App;
using CatsAssistant.App.ViewModels;
using CatsAssistant.Collector;
using CatsAssistant.Store;

namespace CatsAssistant.Tests.App;

public class CaptureSettingsViewModelTests
{
    [Fact]
    public void Constructor_WithNoSettings_UsesDefaults()
    {
        var viewModel = new CaptureSettingsViewModel();

        Assert.Equal((int)IdleDetector.DefaultThreshold.TotalMinutes, viewModel.IdleThresholdMinutes);
        Assert.Equal(15, viewModel.MinBlockMinutes);
        Assert.False(viewModel.IsAutostartEnabled);
        Assert.False(viewModel.IsPaused);
        Assert.Null(viewModel.RestartNotice);
    }

    [Fact]
    public void IncrementIdleThreshold_PersistsAndSignalsRestart()
    {
        var settings = new FakeSettingsRepository();
        var viewModel = new CaptureSettingsViewModel(settings);

        viewModel.IncrementIdleThresholdCommand.Execute(null);

        Assert.Equal(6, viewModel.IdleThresholdMinutes);
        Assert.Equal("6", settings.Get(CaptureSettingsViewModel.IdleThresholdMinutesKey));
        Assert.NotNull(viewModel.RestartNotice);
    }

    [Fact]
    public void DecrementIdleThreshold_NeverGoesBelowOne()
    {
        var viewModel = new CaptureSettingsViewModel();

        for (var i = 0; i < 10; i++)
        {
            viewModel.DecrementIdleThresholdCommand.Execute(null);
        }

        Assert.Equal(1, viewModel.IdleThresholdMinutes);
    }

    [Fact]
    public void SelectMinBlockCommand_PersistsChoice()
    {
        var settings = new FakeSettingsRepository();
        var viewModel = new CaptureSettingsViewModel(settings);

        viewModel.SelectMinBlockCommand.Execute("30");

        Assert.Equal(30, viewModel.MinBlockMinutes);
        Assert.Equal("30", settings.Get(CaptureSettingsViewModel.MinBlockMinutesKey));
    }

    [Fact]
    public void IsAutostartEnabled_TogglesStartupRegistrationImmediately()
    {
        var startupRegistration = new FakeStartupRegistration();
        var viewModel = new CaptureSettingsViewModel(startupRegistration: startupRegistration, executablePath: "C:\\app.exe");

        viewModel.IsAutostartEnabled = true;
        Assert.True(startupRegistration.IsEnabled());

        viewModel.IsAutostartEnabled = false;
        Assert.False(startupRegistration.IsEnabled());
    }

    [Fact]
    public void IsPaused_StopsAndStartsCollectorImmediately_NoRestartNotice()
    {
        var settings = new FakeSettingsRepository();
        var collector = new FakeCollector();
        var viewModel = new CaptureSettingsViewModel(settings, collector);

        viewModel.IsPaused = true;

        Assert.False(collector.IsRunning);
        Assert.Equal("true", settings.Get(CaptureSettingsViewModel.PausedKey));
        Assert.Null(viewModel.RestartNotice);

        viewModel.IsPaused = false;

        Assert.True(collector.IsRunning);
        Assert.Equal("false", settings.Get(CaptureSettingsViewModel.PausedKey));
    }

    private sealed class FakeSettingsRepository : ISettingsRepository
    {
        private readonly Dictionary<string, string> _values = [];

        public string? Get(string key) => _values.GetValueOrDefault(key);

        public void Set(string key, string value) => _values[key] = value;
    }

    private sealed class FakeStartupRegistration : IStartupRegistration
    {
        private bool _enabled;

        public bool IsEnabled() => _enabled;

        public void Enable(string executablePath) => _enabled = true;

        public void Disable() => _enabled = false;
    }

    private sealed class FakeCollector : IActivityCollectorControl
    {
        public bool IsRunning { get; private set; } = true;

        public void Start() => IsRunning = true;

        public void Stop() => IsRunning = false;
    }
}
