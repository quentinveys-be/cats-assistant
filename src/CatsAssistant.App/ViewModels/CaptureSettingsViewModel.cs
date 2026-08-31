using System.Globalization;
using CatsAssistant.App.Mvvm;
using CatsAssistant.Collector;
using CatsAssistant.Store;

namespace CatsAssistant.App.ViewModels;

/// <summary>
/// Onglet Capture des Paramètres (issue #23). Le seuil d'inactivité et la durée minimale de bloc sont
/// lus une seule fois par <see cref="ActivityCollector"/>/le futur Correlator au démarrage : les persister
/// ici ne les rend donc effectifs qu'après redémarrage (<see cref="RestartNotice"/>). L'autostart et la
/// pause de capture, eux, agissent immédiatement sur <see cref="StartupRegistration"/>/<see cref="IActivityCollectorControl"/>.
/// </summary>
public sealed class CaptureSettingsViewModel : ObservableObject
{
    public const string IdleThresholdMinutesKey = "capture.idle_threshold_minutes";
    public const string MinBlockMinutesKey = "capture.min_block_minutes";
    public const string PausedKey = "capture.paused";

    public static readonly IReadOnlyList<int> MinBlockChoices = [10, 15, 30];

    private const int MinIdleThresholdMinutes = 1;
    private const int MaxIdleThresholdMinutes = 60;
    private const string RestartRequiredMessage = "Redémarrez CATS Assistant pour appliquer ce réglage.";

    private readonly ISettingsRepository? _settings;
    private readonly IActivityCollectorControl? _collector;
    private readonly IStartupRegistration? _startupRegistration;
    private readonly string _executablePath;

    private int _idleThresholdMinutes;
    private int _minBlockMinutes;
    private bool _isAutostartEnabled;
    private bool _isPaused;
    private string? _restartNotice;

    public CaptureSettingsViewModel(
        ISettingsRepository? settings = null,
        IActivityCollectorControl? collector = null,
        IStartupRegistration? startupRegistration = null,
        string? executablePath = null)
    {
        _settings = settings;
        _collector = collector;
        _startupRegistration = startupRegistration;
        _executablePath = executablePath ?? Environment.ProcessPath ?? string.Empty;

        _idleThresholdMinutes = SettingsInt.ParseOrDefault(
            _settings?.Get(IdleThresholdMinutesKey), (int)IdleDetector.DefaultThreshold.TotalMinutes);
        _minBlockMinutes = SettingsInt.ParseOrDefault(_settings?.Get(MinBlockMinutesKey), MinBlockChoices[1]);
        _isAutostartEnabled = _startupRegistration?.IsEnabled() ?? false;
        _isPaused = _settings?.Get(PausedKey) == "true";

        IncrementIdleThresholdCommand = new RelayCommand(
            () => IdleThresholdMinutes++, () => IdleThresholdMinutes < MaxIdleThresholdMinutes);
        DecrementIdleThresholdCommand = new RelayCommand(
            () => IdleThresholdMinutes--, () => IdleThresholdMinutes > MinIdleThresholdMinutes);
        SelectMinBlockCommand = new RelayCommand(value => MinBlockMinutes = Convert.ToInt32(value, CultureInfo.InvariantCulture));
    }

    public RelayCommand IncrementIdleThresholdCommand { get; }

    public RelayCommand DecrementIdleThresholdCommand { get; }

    public RelayCommand SelectMinBlockCommand { get; }

    public int IdleThresholdMinutes
    {
        get => _idleThresholdMinutes;
        set
        {
            var clamped = Math.Clamp(value, MinIdleThresholdMinutes, MaxIdleThresholdMinutes);
            if (SetProperty(ref _idleThresholdMinutes, clamped))
            {
                _settings?.Set(IdleThresholdMinutesKey, clamped.ToString(CultureInfo.InvariantCulture));
                RestartNotice = RestartRequiredMessage;
            }
        }
    }

    public int MinBlockMinutes
    {
        get => _minBlockMinutes;
        set
        {
            if (SetProperty(ref _minBlockMinutes, value))
            {
                _settings?.Set(MinBlockMinutesKey, value.ToString(CultureInfo.InvariantCulture));
                RestartNotice = RestartRequiredMessage;
            }
        }
    }

    public bool IsAutostartEnabled
    {
        get => _isAutostartEnabled;
        set
        {
            if (SetProperty(ref _isAutostartEnabled, value))
            {
                if (value)
                {
                    _startupRegistration?.Enable(_executablePath);
                }
                else
                {
                    _startupRegistration?.Disable();
                }
            }
        }
    }

    public bool IsPaused
    {
        get => _isPaused;
        set
        {
            if (SetProperty(ref _isPaused, value))
            {
                _settings?.Set(PausedKey, value ? "true" : "false");
                if (value)
                {
                    _collector?.Stop();
                }
                else
                {
                    _collector?.Start();
                }
            }
        }
    }

    public string? RestartNotice
    {
        get => _restartNotice;
        private set => SetProperty(ref _restartNotice, value);
    }
}
