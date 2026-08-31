using System.Globalization;
using CatsAssistant.App.Mvvm;
using CatsAssistant.Store;

namespace CatsAssistant.App.ViewModels;

/// <summary>
/// Une carte du panneau « Lignes CATS proposées » (issue #18), portée par une ligne <see cref="TimeBlock"/>.
/// Le cycle de statut manipulable au clic ne couvre que Validé ⇄ Proposé (comme le prototype :
/// <c>onValidate</c> ne fait que basculer 'validated'/'proposed') ; le passage à Modifié vient du dialogue
/// d'édition (issue #19), ouvert par le bouton « Modifier ».
/// </summary>
public sealed class CatsLineViewModel : ObservableObject
{
    private readonly ITimeBlockRepository _repository;
    private readonly Action _onChanged;
    private TimeBlockRow _row;

    public CatsLineViewModel(
        TimeBlockRow row, ITimeBlockRepository repository, Action onChanged, Action<CatsLineViewModel>? onEdit = null)
    {
        _row = row;
        _repository = repository;
        _onChanged = onChanged;

        ToggleValidateCommand = new RelayCommand(ToggleValidate, () => Status != TimeBlockStatus.Submitted);
        EditCommand = new RelayCommand(() => onEdit?.Invoke(this), () => onEdit is not null);
    }

    public long Id => _row.Id;

    public TimeBlock Block => _row.TimeBlock;

    public double DurationHours => _row.TimeBlock.DurationHours;

    public string DurationLabel => FormatHours(DurationHours);

    public string KeyDisplay => _row.TimeBlock.JiraKey ?? "Aucun ticket";

    public bool IsUncorrelated => _row.TimeBlock.JiraKey is null;

    public string Note => _row.TimeBlock.Note;

    public string Posid => _row.TimeBlock.Posid;

    public string Zwpid => _row.TimeBlock.Zwpid;

    // Neutre tant que la vérification ValueHelpList (Phase 4) n'est pas disponible (issue #18).
    public string VerificationLabel => "À vérifier";

    public TimeBlockStatus Status => _row.TimeBlock.Status;

    public string StatusLabel => Status switch
    {
        TimeBlockStatus.Proposed => "Proposé",
        TimeBlockStatus.Edited => "Modifié",
        TimeBlockStatus.Validated => "Validé",
        TimeBlockStatus.Submitted => "Soumis ✓",
        _ => throw new ArgumentOutOfRangeException(nameof(Status)),
    };

    public bool IsSubmitted => Status == TimeBlockStatus.Submitted;

    public bool HasActions => !IsSubmitted;

    public string ValidateLabel => Status == TimeBlockStatus.Validated ? "Validé ✓" : "Valider";

    public RelayCommand ToggleValidateCommand { get; }

    public RelayCommand EditCommand { get; }

    public void Validate()
    {
        if (Status != TimeBlockStatus.Submitted && Status != TimeBlockStatus.Validated)
        {
            SetStatus(TimeBlockStatus.Validated);
        }
    }

    private void ToggleValidate() =>
        SetStatus(Status == TimeBlockStatus.Validated ? TimeBlockStatus.Proposed : TimeBlockStatus.Validated);

    private void SetStatus(TimeBlockStatus status)
    {
        var updated = _row.TimeBlock with { Status = status };
        _repository.Update(_row.Id, updated);
        _row = _row with { TimeBlock = updated };

        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(IsSubmitted));
        OnPropertyChanged(nameof(HasActions));
        OnPropertyChanged(nameof(ValidateLabel));

        _onChanged();
    }

    private static string FormatHours(double hours)
    {
        var totalMinutes = (int)Math.Round(hours * 60, MidpointRounding.AwayFromZero);
        return string.Create(CultureInfo.InvariantCulture, $"{totalMinutes / 60}:{totalMinutes % 60:00}");
    }
}
