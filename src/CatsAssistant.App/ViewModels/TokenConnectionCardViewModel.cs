using CatsAssistant.App.Mvvm;
using CatsAssistant.Secrets;
using CatsAssistant.Store;

namespace CatsAssistant.App.ViewModels;

/// <summary>
/// Carte "Connexions" d'un token secret (JIRA/GitLab, issue #24). Le remplacement transite uniquement par
/// <see cref="ISecretVault"/> (jamais en clair sur disque, jamais loggé) ; seuls un suffixe non sensible
/// (4 derniers caractères) et des horodatages non secrets sont conservés via <see cref="ISettingsRepository"/>
/// pour l'affichage, sans jamais réécrire le token complet hors du coffre.
/// </summary>
public sealed class TokenConnectionCardViewModel : ObservableObject
{
    private readonly ISecretVault _vault;
    private readonly ISettingsRepository _settingsRepository;
    private readonly SecretName _secretName;
    private readonly string _suffixSettingKey;
    private readonly string _updatedSettingKey;
    private readonly string? _expirySettingKey;
    private readonly Func<DateTimeOffset> _utcNow;

    private bool _isReplacing;
    private string? _pendingToken;
    private DateTime? _pendingExpiryDate;
    private string? _errorMessage;
    private string? _lastSyncText;

    private ConnectionStatus _status;
    private string _statusLabel = "";
    private string _maskedSuffix = "";
    private string? _detailText;

    public TokenConnectionCardViewModel(
        string name,
        string subtitle,
        ISecretVault vault,
        ISettingsRepository settingsRepository,
        SecretName secretName,
        string settingKeyPrefix,
        bool tracksExpiry,
        Func<DateTimeOffset>? utcNow = null)
    {
        Name = name;
        Subtitle = subtitle;
        _vault = vault;
        _settingsRepository = settingsRepository;
        _secretName = secretName;
        _suffixSettingKey = $"{settingKeyPrefix}.suffix";
        _updatedSettingKey = $"{settingKeyPrefix}.updatedUtc";
        _expirySettingKey = tracksExpiry ? $"{settingKeyPrefix}.expiresUtc" : null;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);

        ReplaceCommand = new RelayCommand(BeginReplace);
        ConfirmReplaceCommand = new RelayCommand(() => _ = ConfirmReplaceAsync(), () => !string.IsNullOrWhiteSpace(PendingToken));
        CancelReplaceCommand = new RelayCommand(CancelReplace);

        Refresh();
    }

    public string Name { get; }

    public string Subtitle { get; }

    public bool TracksExpiry => _expirySettingKey is not null;

    public RelayCommand ReplaceCommand { get; }

    public RelayCommand ConfirmReplaceCommand { get; }

    public RelayCommand CancelReplaceCommand { get; }

    public bool IsReplacing
    {
        get => _isReplacing;
        private set => SetProperty(ref _isReplacing, value);
    }

    /// <summary>Jamais persisté tel quel : consommé puis effacé par <see cref="ConfirmReplaceAsync"/>.</summary>
    public string? PendingToken
    {
        get => _pendingToken;
        set => SetProperty(ref _pendingToken, value);
    }

    public DateTime? PendingExpiryDate
    {
        get => _pendingExpiryDate;
        set => SetProperty(ref _pendingExpiryDate, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>Fixé par le VM parent (issue #24) depuis <see cref="SyncService"/> : hors périmètre de cette carte.</summary>
    public string? LastSyncText
    {
        get => _lastSyncText;
        set
        {
            if (SetProperty(ref _lastSyncText, value))
            {
                OnPropertyChanged(nameof(SubtitleLine));
            }
        }
    }

    /// <summary>Ligne unique affichée sous le titre de la carte : évite de jongler avec plusieurs <c>Run</c> conditionnels en XAML.</summary>
    public string SubtitleLine => string.Join(" · ", new[] { Subtitle, DetailText, LastSyncText }.Where(s => !string.IsNullOrEmpty(s)));

    public ConnectionStatus Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public string StatusLabel
    {
        get => _statusLabel;
        private set => SetProperty(ref _statusLabel, value);
    }

    public string MaskedSuffix
    {
        get => _maskedSuffix;
        private set => SetProperty(ref _maskedSuffix, value);
    }

    public string? DetailText
    {
        get => _detailText;
        private set
        {
            if (SetProperty(ref _detailText, value))
            {
                OnPropertyChanged(nameof(SubtitleLine));
            }
        }
    }

    private void BeginReplace()
    {
        PendingToken = null;
        PendingExpiryDate = null;
        ErrorMessage = null;
        IsReplacing = true;
    }

    private void CancelReplace()
    {
        PendingToken = null;
        PendingExpiryDate = null;
        ErrorMessage = null;
        IsReplacing = false;
    }

    public async Task ConfirmReplaceAsync()
    {
        if (string.IsNullOrWhiteSpace(PendingToken))
        {
            return;
        }

        var token = PendingToken;
        var expiry = PendingExpiryDate;
        ErrorMessage = null;

        try
        {
            // Bloquant : la dérivation de clé attend le touch YubiKey (docs/adr/D6) — jamais sur le thread UI.
            await Task.Run(() => _vault.Store(_secretName, token));
        }
        catch (YubiKeyNotPresentException)
        {
            ErrorMessage = "Aucune YubiKey détectée. Branchez-la puis réessayez.";
            return;
        }

        var suffix = token.Length >= 4 ? token[^4..] : token;
        _settingsRepository.Set(_suffixSettingKey, suffix);
        _settingsRepository.Set(_updatedSettingKey, _utcNow().ToString("O"));
        if (_expirySettingKey is not null && expiry is not null)
        {
            _settingsRepository.Set(_expirySettingKey, new DateTimeOffset(expiry.Value.Date, TimeSpan.Zero).ToString("O"));
        }

        PendingToken = null;
        PendingExpiryDate = null;
        IsReplacing = false;
        Refresh();
    }

    private void Refresh()
    {
        var suffix = _settingsRepository.Get(_suffixSettingKey);
        var updatedRaw = _settingsRepository.Get(_updatedSettingKey);
        var expiryRaw = _expirySettingKey is not null ? _settingsRepository.Get(_expirySettingKey) : null;
        var expiresUtc = expiryRaw is not null ? DateTimeOffset.Parse(expiryRaw) : (DateTimeOffset?)null;

        if (suffix is null)
        {
            Status = ConnectionStatus.NotConfigured;
            StatusLabel = "non configuré";
            MaskedSuffix = "";
            DetailText = "Aucun identifiant enregistré.";
            return;
        }

        MaskedSuffix = $"••••••••••••{suffix}";
        var updatedText = updatedRaw is not null
            ? $"remplacé le {DateTimeOffset.Parse(updatedRaw).ToLocalTime():dd/MM/yyyy}"
            : null;

        if (expiresUtc is not null && expiresUtc.Value <= _utcNow())
        {
            Status = ConnectionStatus.Expired;
            StatusLabel = "expiré";
            DetailText = $"Token expiré le {expiresUtc.Value.ToLocalTime():dd/MM/yyyy}";
            return;
        }

        Status = ConnectionStatus.Connected;
        StatusLabel = "connecté";
        DetailText = expiresUtc is not null
            ? $"{updatedText} · expire le {expiresUtc.Value.ToLocalTime():dd/MM/yyyy}"
            : updatedText;
    }
}
