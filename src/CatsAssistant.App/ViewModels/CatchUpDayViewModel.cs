using System.Globalization;
using System.Windows.Input;
using CatsAssistant.App.Mvvm;
using CatsAssistant.App.Services;
using CatsAssistant.Store;

namespace CatsAssistant.App.ViewModels;

/// <summary>Carte d'une journée à l'écran Rattrapage (issue #22) : durée proposée/attendue, jauge, statut et
/// actions "Ouvrir la journée" / "Valider ce jour". La couleur associée au statut est dérivée par la vue
/// (DataTrigger sur <see cref="Status"/>), pas ici — cf. convention de <see cref="ConnectorPillViewModel"/>.</summary>
public sealed class CatchUpDayViewModel : ObservableObject
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private readonly ITimeBlockRepository _repository;
    private readonly Action _onValidated;
    private CatchUpDayInfo _info;
    private bool _isCurrentWalkDay;

    public CatchUpDayViewModel(CatchUpDayInfo info, ITimeBlockRepository repository, DateOnly today, Action<DateOnly> openDay, Action onValidated)
    {
        _info = info;
        _repository = repository;
        _onValidated = onValidated;

        LongLabel = FormatLongLabel(info.Date, today);
        OpenCommand = new RelayCommand(() => openDay(info.Date));
        ValidateCommand = new RelayCommand(Validate, () => Status != CatchUpDayStatus.Validated);
    }

    public DateOnly Date => _info.Date;

    public string LongLabel { get; }

    public string Note => _info.Note;

    public string ProposedLabel => CatchUpDayCalculator.FormatHours(_info.ProposedHours);

    public string ExpectedLabel => CatchUpDayCalculator.FormatHours(_info.ExpectedHours);

    public int PercentValue => _info.ExpectedHours <= 0
        ? 0
        : (int)Math.Round(_info.ProposedHours / _info.ExpectedHours * 100, MidpointRounding.AwayFromZero);

    public string PercentLabel => $"{PercentValue} %";

    /// <summary>Ton du pourcentage/de la jauge — "Success" ≥100 %, "Caution" ≥85 %, "Critical" en dessous.</summary>
    public string PercentToneKey => PercentValue >= 100 ? "Success" : PercentValue >= 85 ? "Caution" : "Critical";

    public double GaugeFraction => Math.Clamp(PercentValue / 100.0, 0, 1);

    public double GaugeRemainder => 1 - GaugeFraction;

    public CatchUpDayStatus Status => _info.Status;

    public string StatusLabel => Status switch
    {
        CatchUpDayStatus.Incomplete => "Incomplet",
        CatchUpDayStatus.NeedsReview => "À vérifier",
        CatchUpDayStatus.ReadyToValidate => "Prêt à valider",
        CatchUpDayStatus.InProgress => "En cours",
        CatchUpDayStatus.Validated => "Validé",
        _ => throw new ArgumentOutOfRangeException(),
    };

    public string ValidateLabel => Status == CatchUpDayStatus.Validated ? "Validé ✓" : "Valider ce jour";

    public bool IsCurrentWalkDay
    {
        get => _isCurrentWalkDay;
        set => SetProperty(ref _isCurrentWalkDay, value);
    }

    public ICommand OpenCommand { get; }

    public RelayCommand ValidateCommand { get; }

    private void Validate()
    {
        if (Status == CatchUpDayStatus.Validated)
        {
            return;
        }

        CatchUpDayCalculator.ValidateDay(_repository, _info.Blocks);
        _info = _info with { Status = CatchUpDayStatus.Validated };

        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(ValidateLabel));
        CommandManager.InvalidateRequerySuggested();

        _onValidated();
    }

    private static string FormatLongLabel(DateOnly date, DateOnly today)
    {
        var label = date.ToDateTime(TimeOnly.MinValue).ToString("dddd d MMMM yyyy", French);
        return date == today ? $"{label} · aujourd'hui" : label;
    }
}
