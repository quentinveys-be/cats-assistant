using System.Globalization;
using CatsAssistant.App.Mvvm;
using CatsAssistant.App.Services;
using CatsAssistant.Store;

namespace CatsAssistant.App.ViewModels;

/// <summary>
/// Écran Rattrapage (issue #22) : jours ouvrés passés non complétés, validables un par un ou via le
/// mode "Tout valider jour par jour". Sans <see cref="ITimeBlockRepository"/> (coffre métier verrouillé),
/// l'écran reste vide plutôt que de planter — cohérent avec le reste de l'app (docs/adr/D6).
/// </summary>
public sealed class CatchUpViewModel : ScreenViewModelBase
{
    private readonly ISettingsRepository? _settings;
    private readonly IReadOnlyList<CatchUpDayViewModel> _walkableDays;
    private int _walkIndex = -1;
    private string _subtitle = string.Empty;

    public CatchUpViewModel(
        ITimeBlockRepository? repository = null,
        ISettingsRepository? settings = null,
        Action<DateOnly>? openDay = null,
        DateOnly? today = null)
        : base("Rattrapage")
    {
        _settings = settings;
        var todayDate = today ?? DateOnly.FromDateTime(DateTime.Today);
        var openDayAction = openDay ?? (_ => { });

        Days = repository is null
            ? []
            : CatchUpDayCalculator.ComputeIncompleteDays(repository, todayDate, ExpectedHoursPerDay)
                .Select(info => new CatchUpDayViewModel(info, repository, todayDate, openDayAction, OnDayValidated))
                .ToList();

        _walkableDays = Days.Where(d => d.Status != CatchUpDayStatus.InProgress).ToList();

        StartWalkCommand = new RelayCommand(StartWalk, () => !IsWalking && PendingCount > 0);
        WalkValidateCommand = new RelayCommand(WalkValidate, () => IsWalking);
        WalkSkipCommand = new RelayCommand(WalkSkip, () => IsWalking);

        UpdateSubtitle();
    }

    public IReadOnlyList<CatchUpDayViewModel> Days { get; }

    /// <summary>Nombre de journées non encore validées — alimente le badge de nav/tray (tâche 5 de l'issue #22).</summary>
    public int IncompleteDayCount => PendingCount;

    public string Subtitle
    {
        get => _subtitle;
        private set => SetProperty(ref _subtitle, value);
    }

    public bool IsWalking => _walkIndex >= 0 && _walkIndex < _walkableDays.Count;

    public string WalkLabel => $"Journée {Math.Min(_walkIndex + 1, _walkableDays.Count)} sur {_walkableDays.Count}";

    public string WalkDayLabel => IsWalking ? _walkableDays[_walkIndex].LongLabel : string.Empty;

    public RelayCommand StartWalkCommand { get; }

    public RelayCommand WalkValidateCommand { get; }

    public RelayCommand WalkSkipCommand { get; }

    private int PendingCount => _walkableDays.Count(d => d.Status != CatchUpDayStatus.Validated);

    private double ExpectedHoursPerDay => WorkScheduleSettings.ExpectedHoursPerDay(_settings);

    private double ExpectedHoursPerWeek => WorkScheduleSettings.ExpectedHoursPerWeek(_settings);

    private void StartWalk()
    {
        if (PendingCount == 0)
        {
            return;
        }

        _walkIndex = 0;
        HighlightCurrentWalkDay();
        RaiseWalkChanged();
    }

    private void WalkValidate()
    {
        if (!IsWalking)
        {
            return;
        }

        _walkableDays[_walkIndex].ValidateCommand.Execute(null);
        AdvanceWalk();
    }

    private void WalkSkip()
    {
        if (!IsWalking)
        {
            return;
        }

        AdvanceWalk();
    }

    private void AdvanceWalk()
    {
        ClearCurrentWalkDay();
        _walkIndex++;
        if (_walkIndex >= _walkableDays.Count)
        {
            _walkIndex = -1;
        }

        HighlightCurrentWalkDay();
        RaiseWalkChanged();
    }

    private void HighlightCurrentWalkDay()
    {
        if (IsWalking)
        {
            _walkableDays[_walkIndex].IsCurrentWalkDay = true;
        }
    }

    private void ClearCurrentWalkDay()
    {
        if (IsWalking)
        {
            _walkableDays[_walkIndex].IsCurrentWalkDay = false;
        }
    }

    private void RaiseWalkChanged()
    {
        OnPropertyChanged(nameof(IsWalking));
        OnPropertyChanged(nameof(WalkLabel));
        OnPropertyChanged(nameof(WalkDayLabel));
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }

    private void OnDayValidated()
    {
        OnPropertyChanged(nameof(IncompleteDayCount));
        UpdateSubtitle();
    }

    private void UpdateSubtitle()
    {
        var pending = _walkableDays.Where(d => d.Status != CatchUpDayStatus.Validated).ToList();
        if (pending.Count == 0)
        {
            Subtitle = "Aucune journée à rattraper.";
            return;
        }

        var plural = pending.Count > 1 ? "s" : string.Empty;
        Subtitle =
            $"{pending.Count} journée{plural} non complétée{plural} depuis le {pending[0].LongLabel} · " +
            $"{FormatWeekly(ExpectedHoursPerWeek)} h/semaine, {CatchUpDayCalculator.FormatHours(ExpectedHoursPerDay)} attendues par jour";
    }

    private static string FormatWeekly(double hours) =>
        hours.ToString(hours == Math.Floor(hours) ? "0" : "0.#", CultureInfo.InvariantCulture);
}
