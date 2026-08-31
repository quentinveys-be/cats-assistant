using System.Globalization;
using System.IO;
using CatsAssistant.App.Mvvm;
using CatsAssistant.Store;

namespace CatsAssistant.App.ViewModels;

/// <summary>
/// Onglet Données des Paramètres (issue #23). Contrairement au seuil d'inactivité (Capture), changer la
/// rétention ici lance immédiatement <see cref="ActivityEventRetentionPurger"/> avec le nouveau seuil —
/// effectif sans redémarrage, pas seulement persisté.
/// </summary>
public sealed class DataSettingsViewModel : ObservableObject
{
    public const string RetentionDaysKey = "data.retention_days";

    public static readonly IReadOnlyList<int> RetentionChoices = [30, 90, 180];

    private readonly ISettingsRepository? _settings;
    private readonly IActivityEventRepository? _events;
    private readonly string? _databasePath;

    private int _retentionDays;
    private string _databaseSizeText = "—";
    private int _eventCount;

    public DataSettingsViewModel(
        ISettingsRepository? settings = null,
        IActivityEventRepository? events = null,
        ManualPurgeService? purgeService = null,
        string? databasePath = null)
    {
        _settings = settings;
        _events = events;
        PurgeService = purgeService;
        _databasePath = databasePath;

        _retentionDays = SettingsInt.ParseOrDefault(
            _settings?.Get(RetentionDaysKey), (int)ActivityEventRetentionPurger.DefaultRetention.TotalDays);

        SelectRetentionCommand = new RelayCommand(value => RetentionDays = Convert.ToInt32(value, CultureInfo.InvariantCulture));

        RefreshDatabaseInfo();
    }

    public RelayCommand SelectRetentionCommand { get; }

    /// <summary>Null si la base métier (business.db, time_blocks/rules) n'est pas déverrouillée — le bouton
    /// "Purger les données" reste alors désactivé plutôt que de proposer une purge partielle non annoncée.</summary>
    public ManualPurgeService? PurgeService { get; }

    public bool CanPurge => PurgeService is not null;

    public string DatabasePath => _databasePath ?? string.Empty;

    public int RetentionDays
    {
        get => _retentionDays;
        set
        {
            if (SetProperty(ref _retentionDays, value))
            {
                _settings?.Set(RetentionDaysKey, value.ToString(CultureInfo.InvariantCulture));
                if (_events is not null)
                {
                    new ActivityEventRetentionPurger(_events, TimeSpan.FromDays(value)).Purge();
                    RefreshDatabaseInfo();
                }
            }
        }
    }

    public string DatabaseSizeText
    {
        get => _databaseSizeText;
        private set => SetProperty(ref _databaseSizeText, value);
    }

    public int EventCount
    {
        get => _eventCount;
        private set => SetProperty(ref _eventCount, value);
    }

    public void RefreshDatabaseInfo()
    {
        EventCount = _events?.Count() ?? 0;

        if (_databasePath is not null && File.Exists(_databasePath))
        {
            DatabaseSizeText = FormatSize(new FileInfo(_databasePath).Length);
        }
    }

    private static string FormatSize(long bytes)
    {
        double megabytes = bytes / (1024.0 * 1024.0);
        return megabytes < 0.1
            ? $"{bytes / 1024.0:0.#} Ko"
            : $"{megabytes:0.#} Mo";
    }
}
