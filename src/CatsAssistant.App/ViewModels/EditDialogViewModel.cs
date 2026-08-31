using System.Globalization;
using CatsAssistant.App.Mvvm;
using CatsAssistant.Store;

namespace CatsAssistant.App.ViewModels;

/// <summary>Les 4 variantes du dialogue d'édition du prototype (overlay <c>edition</c>, issue #19).</summary>
public enum EditDialogKind
{
    /// <summary>« Modifier l'activité capturée » (un segment brut de la timeline).</summary>
    CapturedActivity,

    /// <summary>« Modifier la plage CATS » (un regroupement de la colonne droite).</summary>
    CatsRange,

    /// <summary>« Imputer cette plage » (une zone « à imputer » non corrélée).</summary>
    ImputeGap,

    /// <summary>« Modifier la ligne CATS » (une carte du panneau des propositions).</summary>
    CatsLine,
}

public enum EditDialogOutcome
{
    Cancelled,
    Saved,
    Deleted,
}

/// <summary>Sévérité du compteur de note (seuils du prototype : warn &gt; 70, err &gt; 80).</summary>
public enum NoteCounterSeverity
{
    Normal,
    Warning,
    Error,
}

/// <summary>Un ticket JIRA assigné proposé par l'autocomplete (clé + résumé + statut + codes extraits).</summary>
public sealed record TicketSuggestion(string Key, string Summary, string Status, string? Posid, string? Zwpid);

/// <summary>
/// Logique du dialogue d'édition (issue #19), sans dépendance WPF pour rester testable : steppers de plage
/// (pas de 15 min, début &lt; fin) et de durée (pas de 0,25 h), autocomplete des tickets assignés, note avec
/// compteur /80 pré-remplie « KEY - résumé », POSID/ZWPID en lecture seule issus du ticket. La vue ferme la
/// fenêtre via <see cref="RequestClose"/> et le DayViewModel applique <see cref="Outcome"/> aux données.
/// </summary>
public sealed class EditDialogViewModel : ObservableObject
{
    private const int RangeStepMinutes = 15;
    private const double DurationStepHours = 0.25;
    private const double DurationMinHours = 0.25;
    private const double DurationMaxHours = 12;
    private const int NoteMaxLength = 80;
    private const int NoteWarnLength = 70;

    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("fr-FR");

    private readonly IReadOnlyList<TicketSuggestion> _tickets;
    private readonly string? _sourceDetail;

    // Bornes des steppers : 7:00–20:00 comme le prototype, élargies si l'élément édité déborde déjà.
    private readonly int _minMinutes;
    private readonly int _maxMinutes;

    private int _startMinutes;
    private int _endMinutes;
    private double _durationHours;
    private string _query = string.Empty;
    private string _note = string.Empty;
    private bool _isListOpen;
    private TicketSuggestion? _selectedTicket;

    private EditDialogViewModel(
        EditDialogKind kind,
        DateOnly date,
        int startMinutes,
        int endMinutes,
        double durationHours,
        string? initialJiraKey,
        string? initialPosid,
        string? initialZwpid,
        string initialNote,
        string? sourceDetail,
        bool canDelete,
        IReadOnlyList<TicketSuggestion> tickets)
    {
        Kind = kind;
        Date = date;
        _startMinutes = startMinutes;
        _endMinutes = endMinutes;
        _durationHours = durationHours;
        InitialJiraKey = initialJiraKey;
        _sourceDetail = sourceDetail;
        CanDelete = canDelete;
        _tickets = tickets;

        _minMinutes = Math.Min(7 * 60, startMinutes);
        _maxMinutes = Math.Max(20 * 60, endMinutes);

        // Ticket courant présélectionné ; s'il n'est plus dans les tickets assignés, on conserve ses codes
        // tels que portés par l'élément édité plutôt que de les perdre.
        _selectedTicket = initialJiraKey is null
            ? null
            : tickets.FirstOrDefault(t => t.Key == initialJiraKey)
                ?? new TicketSuggestion(initialJiraKey, string.Empty, string.Empty, initialPosid, initialZwpid);
        _query = initialJiraKey ?? string.Empty;
        _note = initialNote;

        StartMinusCommand = new RelayCommand(() => StepStart(-RangeStepMinutes));
        StartPlusCommand = new RelayCommand(() => StepStart(RangeStepMinutes));
        EndMinusCommand = new RelayCommand(() => StepEnd(-RangeStepMinutes));
        EndPlusCommand = new RelayCommand(() => StepEnd(RangeStepMinutes));
        DurationMinusCommand = new RelayCommand(() => StepDuration(-DurationStepHours));
        DurationPlusCommand = new RelayCommand(() => StepDuration(DurationStepHours));
        SaveCommand = new RelayCommand(() => Close(EditDialogOutcome.Saved), () => CanSave);
        DeleteCommand = new RelayCommand(() => Close(EditDialogOutcome.Deleted), () => CanDelete);
        CancelCommand = new RelayCommand(() => Close(EditDialogOutcome.Cancelled));
    }

    public static EditDialogViewModel ForCapturedActivity(
        DateOnly date, DateTime startLocal, DateTime endLocal, string? process, string? jiraKey,
        IReadOnlyList<TicketSuggestion> tickets)
    {
        var ticket = jiraKey is null ? null : tickets.FirstOrDefault(t => t.Key == jiraKey);
        return new EditDialogViewModel(
            EditDialogKind.CapturedActivity, date, MinutesOf(startLocal), MinutesOf(endLocal), 0,
            jiraKey, ticket?.Posid, ticket?.Zwpid,
            initialNote: ticket is null ? string.Empty : $"{ticket.Key} - {ticket.Summary}",
            sourceDetail: process,
            // « Marquer non facturable » = apprendre une règle d'exclusion : issue dédiée, hors périmètre #19.
            canDelete: false,
            tickets);
    }

    public static EditDialogViewModel ForCatsRange(
        DateOnly date, DateTime startLocal, DateTime endLocal, string jiraKey, string? existingNote,
        bool canDelete, IReadOnlyList<TicketSuggestion> tickets)
    {
        var ticket = tickets.FirstOrDefault(t => t.Key == jiraKey);
        return new EditDialogViewModel(
            EditDialogKind.CatsRange, date, MinutesOf(startLocal), MinutesOf(endLocal), 0,
            jiraKey, ticket?.Posid, ticket?.Zwpid,
            initialNote: existingNote ?? (ticket is null ? string.Empty : $"{ticket.Key} - {ticket.Summary}"),
            sourceDetail: null, canDelete, tickets);
    }

    public static EditDialogViewModel ForGap(
        DateOnly date, DateTime startLocal, DateTime endLocal, IReadOnlyList<TicketSuggestion> tickets) =>
        new(EditDialogKind.ImputeGap, date, MinutesOf(startLocal), MinutesOf(endLocal), 0,
            initialJiraKey: null, initialPosid: null, initialZwpid: null, initialNote: string.Empty,
            sourceDetail: null,
            // « Non facturable » = apprendre une règle d'exclusion : issue dédiée, hors périmètre #19.
            canDelete: false,
            tickets);

    public static EditDialogViewModel ForCatsLine(long lineId, TimeBlock line, IReadOnlyList<TicketSuggestion> tickets) =>
        new(EditDialogKind.CatsLine, line.Date, 0, 0, line.DurationHours,
            line.JiraKey, line.Posid, line.Zwpid, line.Note, sourceDetail: null, canDelete: true, tickets)
        {
            LineId = lineId,
            InitialLine = line,
        };

    /// <summary>Fermeture demandée par une action : l'argument devient le DialogResult de la fenêtre.</summary>
    public event Action<bool>? RequestClose;

    public EditDialogKind Kind { get; }

    public DateOnly Date { get; }

    public EditDialogOutcome Outcome { get; private set; } = EditDialogOutcome.Cancelled;

    // Contexte de l'élément édité, relu par DayViewModel pour appliquer les répercussions.
    public long? LineId { get; private init; }

    public TimeBlock? InitialLine { get; private init; }

    public string? InitialJiraKey { get; }

    public DateTime InitialStartUtc { get; private set; }

    public DateTime InitialEndUtc { get; private set; }

    public double InitialRangeHours => (InitialEndUtc - InitialStartUtc).TotalHours;

    public bool HasRange => Kind != EditDialogKind.CatsLine;

    public bool HasDuration => Kind == EditDialogKind.CatsLine;

    public string Title => Kind switch
    {
        EditDialogKind.CapturedActivity => "Modifier l'activité capturée",
        EditDialogKind.CatsRange => "Modifier la plage CATS",
        EditDialogKind.ImputeGap => "Imputer cette plage",
        EditDialogKind.CatsLine => "Modifier la ligne CATS",
        _ => throw new ArgumentOutOfRangeException(nameof(Kind)),
    };

    public string Subtitle
    {
        get
        {
            var dateLabel = Date.ToDateTime(TimeOnly.MinValue).ToString("ddd d MMMM yyyy", Culture);
            return Kind switch
            {
                EditDialogKind.CapturedActivity => $"{dateLabel} · {StartLabel}–{EndLabel} · {_sourceDetail}",
                EditDialogKind.CatsRange => $"{dateLabel} · {StartLabel}–{EndLabel} · plage CATS regroupant les segments captés",
                EditDialogKind.ImputeGap => $"{dateLabel} · {StartLabel}–{EndLabel} · activité non corrélée — aucun ticket détecté",
                _ => $"{dateLabel} · durée agrégée des plages du corrélateur",
            };
        }
    }

    // ---------- plage horaire (block / plage / gap) ----------

    public string StartLabel => FormatClock(_startMinutes);

    public string EndLabel => FormatClock(_endMinutes);

    public DateTime StartUtc => LocalOf(_startMinutes).ToUniversalTime();

    public DateTime EndUtc => LocalOf(_endMinutes).ToUniversalTime();

    public double RangeHours => (_endMinutes - _startMinutes) / 60.0;

    public string RangeHint =>
        $"{FormatHours(RangeHours)} · pas de 15 min · {RangeHours.ToString("0.00", Culture)} h vers SAP";

    public RelayCommand StartMinusCommand { get; }

    public RelayCommand StartPlusCommand { get; }

    public RelayCommand EndMinusCommand { get; }

    public RelayCommand EndPlusCommand { get; }

    // ---------- durée (line) ----------

    public double DurationHours => _durationHours;

    public string DurationLabel => FormatHours(_durationHours);

    public string DurationHint =>
        $"pas de 0,25 h · envoyé à SAP en décimal ({_durationHours.ToString("0.00", Culture)})";

    public RelayCommand DurationMinusCommand { get; }

    public RelayCommand DurationPlusCommand { get; }

    // ---------- ticket JIRA (autocomplete) ----------

    public string Query
    {
        get => _query;
        set
        {
            if (SetProperty(ref _query, value))
            {
                IsListOpen = true;
                OnPropertyChanged(nameof(Suggestions));
                OnPropertyChanged(nameof(NoMatch));
            }
        }
    }

    public bool IsListOpen
    {
        get => _isListOpen;
        set
        {
            if (SetProperty(ref _isListOpen, value))
            {
                OnPropertyChanged(nameof(NoMatch));
            }
        }
    }

    public IReadOnlyList<TicketSuggestion> Suggestions =>
        _tickets
            .Where(t => _query.Length == 0
                || t.Key.Contains(_query, StringComparison.OrdinalIgnoreCase)
                || t.Summary.Contains(_query, StringComparison.OrdinalIgnoreCase))
            .ToList();

    public bool NoMatch => IsListOpen && Suggestions.Count == 0;

    public TicketSuggestion? SelectedTicket => _selectedTicket;

    public string? SelectedKey => _selectedTicket?.Key;

    public string SelectedPosid => _selectedTicket?.Posid ?? string.Empty;

    public string SelectedZwpid => _selectedTicket?.Zwpid ?? string.Empty;

    /// <summary>Choix d'un ticket : clé dans le champ, note pré-remplie « KEY - résumé », codes recopiés.</summary>
    public void SelectTicket(TicketSuggestion ticket)
    {
        _selectedTicket = ticket;
        _query = ticket.Key;
        Note = $"{ticket.Key} - {ticket.Summary}";
        IsListOpen = false;

        OnPropertyChanged(nameof(Query));
        OnPropertyChanged(nameof(Suggestions));
        OnPropertyChanged(nameof(SelectedTicket));
        OnPropertyChanged(nameof(SelectedKey));
        OnPropertyChanged(nameof(SelectedPosid));
        OnPropertyChanged(nameof(SelectedZwpid));
        OnPropertyChanged(nameof(PosidDisplay));
        OnPropertyChanged(nameof(ZwpidDisplay));
        OnPropertyChanged(nameof(VerificationLabel));
        OnPropertyChanged(nameof(VerificationIsWarning));
        OnPropertyChanged(nameof(CanSave));
    }

    // ---------- note ----------

    public string Note
    {
        get => _note;
        set
        {
            if (SetProperty(ref _note, value))
            {
                OnPropertyChanged(nameof(NoteCounter));
                OnPropertyChanged(nameof(NoteSeverity));
            }
        }
    }

    public string NoteCounter => $"{_note.Length} / {NoteMaxLength}";

    public NoteCounterSeverity NoteSeverity => _note.Length > NoteMaxLength
        ? NoteCounterSeverity.Error
        : _note.Length > NoteWarnLength ? NoteCounterSeverity.Warning : NoteCounterSeverity.Normal;

    // ---------- POSID / ZWPID (lecture seule, customfield_10045) ----------

    public string PosidDisplay => string.IsNullOrEmpty(SelectedPosid) ? "—" : SelectedPosid;

    public string ZwpidDisplay => string.IsNullOrEmpty(SelectedZwpid) ? "—" : SelectedZwpid;

    // La vraie vérification ValueHelpList arrive avec le Filler (Phase 4) : en attendant, le badge signale
    // au moins un ticket sans codes extraits de customfield_10045.
    public string VerificationLabel => _selectedTicket is null
        ? "aucun ticket"
        : string.IsNullOrEmpty(SelectedPosid) ? "codes introuvables ⚠" : "À vérifier";

    public bool VerificationIsWarning => _selectedTicket is not null && string.IsNullOrEmpty(SelectedPosid);

    // ---------- actions ----------

    public string SaveLabel => Kind == EditDialogKind.ImputeGap ? "Créer la plage CATS" : "Enregistrer";

    public string DeleteLabel => Kind switch
    {
        EditDialogKind.CapturedActivity => "Marquer non facturable",
        EditDialogKind.CatsRange => "Supprimer la plage",
        EditDialogKind.ImputeGap => "Non facturable",
        _ => "Supprimer",
    };

    /// <summary>Créer une plage sans ticket n'a pas de sens (le prototype ignore le save sans clé).</summary>
    public bool CanSave => Kind != EditDialogKind.ImputeGap || _selectedTicket is not null;

    public bool CanDelete { get; }

    public RelayCommand SaveCommand { get; }

    public RelayCommand DeleteCommand { get; }

    public RelayCommand CancelCommand { get; }

    /// <summary>Mémorise les bornes initiales UTC (appariement des plages manuelles par DayViewModel).</summary>
    public EditDialogViewModel WithInitialRange(DateTime startUtc, DateTime endUtc)
    {
        InitialStartUtc = startUtc;
        InitialEndUtc = endUtc;
        return this;
    }

    private void Close(EditDialogOutcome outcome)
    {
        Outcome = outcome;
        RequestClose?.Invoke(outcome != EditDialogOutcome.Cancelled);
    }

    private void StepStart(int delta)
    {
        var next = Math.Clamp(_startMinutes + delta, _minMinutes, _endMinutes - RangeStepMinutes);
        if (next != _startMinutes)
        {
            _startMinutes = next;
            NotifyRangeChanged();
        }
    }

    private void StepEnd(int delta)
    {
        var next = Math.Clamp(_endMinutes + delta, _startMinutes + RangeStepMinutes, _maxMinutes);
        if (next != _endMinutes)
        {
            _endMinutes = next;
            NotifyRangeChanged();
        }
    }

    private void StepDuration(double delta)
    {
        var next = Math.Clamp(_durationHours + delta, DurationMinHours, DurationMaxHours);
        if (Math.Abs(next - _durationHours) > 0.001)
        {
            _durationHours = next;
            OnPropertyChanged(nameof(DurationHours));
            OnPropertyChanged(nameof(DurationLabel));
            OnPropertyChanged(nameof(DurationHint));
        }
    }

    private void NotifyRangeChanged()
    {
        OnPropertyChanged(nameof(StartLabel));
        OnPropertyChanged(nameof(EndLabel));
        OnPropertyChanged(nameof(RangeHint));
        OnPropertyChanged(nameof(Subtitle));
    }

    private DateTime LocalOf(int minutes) => Date.ToDateTime(TimeOnly.MinValue).AddMinutes(minutes);

    private static int MinutesOf(DateTime local) => (int)local.TimeOfDay.TotalMinutes;

    private static string FormatClock(int minutes) => $"{minutes / 60:00}:{minutes % 60:00}";

    private static string FormatHours(double hours)
    {
        var totalMinutes = (int)Math.Round(hours * 60, MidpointRounding.AwayFromZero);
        return string.Create(CultureInfo.InvariantCulture, $"{totalMinutes / 60}:{totalMinutes % 60:00}");
    }
}
