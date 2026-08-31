using System.Collections.ObjectModel;
using System.Globalization;
using CatsAssistant.App.Mvvm;
using CatsAssistant.App.Timeline;
using CatsAssistant.Connectors;
using CatsAssistant.Correlator;
using CatsAssistant.Store;

namespace CatsAssistant.App.ViewModels;

/// <summary>
/// Écran Journée (issue #17) : timeline capturée branchée sur le Correlator, et panneau « Lignes CATS
/// proposées » (issue #18) branché sur les mêmes <c>time_blocks</c> du jour. Toutes les dépendances sont
/// optionnelles pour dégrader en écran vide quand la base métier est verrouillée (pas de YubiKey), comme
/// le reste de l'app (docs/adr/D6).
/// </summary>
public sealed class DayViewModel : ScreenViewModelBase
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("fr-FR");

    // Journée de travail complète du prototype (docs/design/screens/cats-assistant.dc.html) : 7h36.
    private const double ExpectedDailyHours = 7.6;

    private readonly IActivityEventRepository? _activityEventRepository;
    private readonly ITimeBlockRepository? _timeBlockRepository;
    private readonly ICalendarEventRepository? _calendarEventRepository;
    private readonly IVcsCommitRepository? _vcsCommitRepository;
    private readonly IRuleRepository? _ruleRepository;
    private readonly IJiraTicketRepository? _jiraTicketRepository;
    private readonly ICorrelationEngine _correlationEngine;
    private readonly Action? _navigateToSummary;

    private DateOnly _selectedDate;
    private string _dayLabel = string.Empty;
    private int _incompleteDaysCount;
    private bool _isEmpty = true;
    private double _timelineHeight;
    private string _rawHeader = string.Empty;
    private string _groupHeader = string.Empty;
    private string _totalProposedLabel = FormatHours(0);
    private double _gaugePercent;
    private int _validatedLinesCount;

    public DayViewModel(
        IActivityEventRepository? activityEventRepository = null,
        ITimeBlockRepository? timeBlockRepository = null,
        ICalendarEventRepository? calendarEventRepository = null,
        IVcsCommitRepository? vcsCommitRepository = null,
        IRuleRepository? ruleRepository = null,
        ICorrelationEngine? correlationEngine = null,
        Action? navigateToCatchUp = null,
        Action? navigateToSummary = null,
        IJiraTicketRepository? jiraTicketRepository = null)
        : base("Journée")
    {
        _activityEventRepository = activityEventRepository;
        _timeBlockRepository = timeBlockRepository;
        _calendarEventRepository = calendarEventRepository;
        _vcsCommitRepository = vcsCommitRepository;
        _ruleRepository = ruleRepository;
        _jiraTicketRepository = jiraTicketRepository;
        _correlationEngine = correlationEngine ?? new CorrelationEngine();
        _navigateToSummary = navigateToSummary;

        PreviousDayCommand = new RelayCommand(() => LoadDay(_selectedDate.AddDays(-1)));
        NextDayCommand = new RelayCommand(() => LoadDay(_selectedDate.AddDays(1)));
        TodayCommand = new RelayCommand(() => LoadDay(DateOnly.FromDateTime(DateTime.Now)));
        GoToCatchUpCommand = new RelayCommand(() => navigateToCatchUp?.Invoke(), () => navigateToCatchUp is not null);
        ValidateAllCommand = new RelayCommand(ValidateAll);
        GoToSummaryCommand = new RelayCommand(() => _navigateToSummary?.Invoke());
        EditSegmentCommand = new RelayCommand(p => EditSegment((TimelineSegmentItem)p!));
        EditGroupCommand = new RelayCommand(p => EditGroup((TimelineGroupItem)p!));
        EditGapCommand = new RelayCommand(p => EditGap((TimelineGapItem)p!));

        QuickEntry = new QuickEntryViewModel(jiraTicketRepository, AddManualLine);

        Hours = [];
        Segments = [];
        Groups = [];
        Gaps = [];
        Meetings = [];
        Lines = [];

        LoadDay(DateOnly.FromDateTime(DateTime.Now));
    }

    public RelayCommand PreviousDayCommand { get; }

    public RelayCommand NextDayCommand { get; }

    public RelayCommand TodayCommand { get; }

    public RelayCommand GoToCatchUpCommand { get; }

    public RelayCommand ValidateAllCommand { get; }

    public RelayCommand GoToSummaryCommand { get; }

    public RelayCommand EditSegmentCommand { get; }

    public RelayCommand EditGroupCommand { get; }

    public RelayCommand EditGapCommand { get; }

    /// <summary>
    /// Affichage modal du dialogue d'édition (issue #19), branché par la vue (ShowDialog) et remplaçable en
    /// test. Retourne true quand l'utilisateur a confirmé une action (Enregistrer / Supprimer).
    /// </summary>
    public Func<EditDialogViewModel, bool>? ShowEditDialog { get; set; }

    public ObservableCollection<HourMarkItem> Hours { get; }

    public ObservableCollection<TimelineSegmentItem> Segments { get; }

    public ObservableCollection<TimelineGroupItem> Groups { get; }

    public ObservableCollection<TimelineGapItem> Gaps { get; }

    public ObservableCollection<TimelineMeetingItem> Meetings { get; }

    public ObservableCollection<CatsLineViewModel> Lines { get; }

    public QuickEntryViewModel QuickEntry { get; }

    /// <summary>Jour ciblé par la navigation "Ouvrir la journée" du Rattrapage (issue #22) : assigner une
    /// valeur recharge immédiatement la timeline sur ce jour.</summary>
    public DateOnly? SelectedDate
    {
        get => _selectedDate;
        set
        {
            if (value.HasValue)
            {
                LoadDay(value.Value);
            }
        }
    }

    public string DayLabel
    {
        get => _dayLabel;
        private set => SetProperty(ref _dayLabel, value);
    }

    public int IncompleteDaysCount
    {
        get => _incompleteDaysCount;
        private set
        {
            if (SetProperty(ref _incompleteDaysCount, value))
            {
                OnPropertyChanged(nameof(HasIncompleteDays));
            }
        }
    }

    public bool HasIncompleteDays => IncompleteDaysCount > 0;

    public bool IsEmpty
    {
        get => _isEmpty;
        private set => SetProperty(ref _isEmpty, value);
    }

    public double TimelineHeight
    {
        get => _timelineHeight;
        private set => SetProperty(ref _timelineHeight, value);
    }

    public string RawHeader
    {
        get => _rawHeader;
        private set => SetProperty(ref _rawHeader, value);
    }

    public string GroupHeader
    {
        get => _groupHeader;
        private set => SetProperty(ref _groupHeader, value);
    }

    public string ExpectedLabel { get; } = FormatHours(ExpectedDailyHours);

    public string TotalProposedLabel
    {
        get => _totalProposedLabel;
        private set => SetProperty(ref _totalProposedLabel, value);
    }

    public double GaugePercent
    {
        get => _gaugePercent;
        private set => SetProperty(ref _gaugePercent, value);
    }

    public int ValidatedLinesCount
    {
        get => _validatedLinesCount;
        private set => SetProperty(ref _validatedLinesCount, value);
    }

    private void LoadDay(DateOnly date)
    {
        _selectedDate = date;
        DayLabel = date.ToDateTime(TimeOnly.MinValue).ToString("ddd d MMMM yyyy", Culture);

        var timeline = BuildTimeline(date);

        Hours.Clear();
        foreach (var hour in timeline.Hours) Hours.Add(HourMarkItem.From(hour));

        Segments.Clear();
        foreach (var segment in timeline.Segments) Segments.Add(TimelineSegmentItem.From(segment));

        Groups.Clear();
        foreach (var group in timeline.Groups) Groups.Add(TimelineGroupItem.From(group));

        Gaps.Clear();
        foreach (var gap in timeline.Gaps) Gaps.Add(TimelineGapItem.From(gap));

        Meetings.Clear();
        foreach (var meeting in timeline.Meetings) Meetings.Add(TimelineMeetingItem.From(meeting));

        IsEmpty = timeline.IsEmpty;
        TimelineHeight = timeline.HeightPx;
        RawHeader = $"{timeline.Segments.Count} segments captés";
        GroupHeader = $"{timeline.Groups.Count} plages CATS";

        IncompleteDaysCount = _timeBlockRepository is null
            ? 0
            : IncompleteDaysCounter.CountIncompleteWeekdays(_timeBlockRepository, DateOnly.FromDateTime(DateTime.Now));

        Lines.Clear();
        if (_timeBlockRepository is not null)
        {
            foreach (var row in _timeBlockRepository.GetByDateRange(date, date))
            {
                Lines.Add(new CatsLineViewModel(row, _timeBlockRepository, RecomputeLineAggregates, EditLine));
            }
        }

        RecomputeLineAggregates();
    }

    private DayTimeline BuildTimeline(DateOnly date)
    {
        if (_activityEventRepository is null)
        {
            return DayTimeline.Empty;
        }

        var dayStartUtc = date.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
        var dayEndUtc = date.ToDateTime(TimeOnly.MinValue).AddDays(1).ToUniversalTime();

        var activityEvents = _activityEventRepository.GetByDateRange(dayStartUtc, dayEndUtc);
        var meetings = _calendarEventRepository?.GetByDateRange(dayStartUtc, dayEndUtc) ?? [];
        var commits = _vcsCommitRepository?.GetByDateRange(new DateTimeOffset(dayStartUtc), new DateTimeOffset(dayEndUtc)) ?? [];
        var rules = _ruleRepository?.GetAll() ?? [];
        var timeBlocksForDay = _timeBlockRepository?.GetByDateRange(date, date) ?? [];

        var correlation = _correlationEngine.Correlate(activityEvents, commits, meetings, rules: rules);

        return DayTimelineBuilder.Build(activityEvents, correlation, meetings, timeBlocksForDay);
    }

    // Encodage rapide (issue #20) : ligne manuelle sans activité captée, statut 'edited' et durée
    // manuelle — la corrélation ne la recalcule jamais (les lignes viennent telles quelles du repo).
    private void AddManualLine(JiraTicket ticket, double durationHours, string note)
    {
        if (_timeBlockRepository is null)
        {
            return;
        }

        var startUtc = _selectedDate.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
        var block = new TimeBlock(
            _selectedDate,
            startUtc,
            startUtc.AddHours(durationHours),
            "Encodage manuel",
            ticket.Key,
            ticket.Posid ?? string.Empty,
            ticket.Zwpid ?? string.Empty,
            note,
            durationHours,
            TimeBlockStatus.Edited,
            SapCounter: null);

        var id = _timeBlockRepository.Insert(block);
        Lines.Add(new CatsLineViewModel(new TimeBlockRow(id, block), _timeBlockRepository, RecomputeLineAggregates, EditLine));
        RecomputeLineAggregates();
    }

    private void ValidateAll()
    {
        foreach (var line in Lines)
        {
            line.Validate();
        }
    }

    // ---------- dialogue d'édition (issue #19) ----------

    private void EditSegment(TimelineSegmentItem item) =>
        RunEditDialog(EditDialogViewModel
            .ForCapturedActivity(_selectedDate, item.StartLocal, item.EndLocal,
                item.Process ?? "Inactivité", item.JiraKey, LoadTicketSuggestions())
            .WithInitialRange(item.StartLocal.ToUniversalTime(), item.EndLocal.ToUniversalTime()));

    private void EditGroup(TimelineGroupItem item)
    {
        var startUtc = item.StartLocal.ToUniversalTime();
        var endUtc = item.EndLocal.ToUniversalTime();
        var rows = _timeBlockRepository?.GetByDateRange(_selectedDate, _selectedDate) ?? [];
        var existing = rows.FirstOrDefault(r =>
            (r.TimeBlock.StartUtc == startUtc && r.TimeBlock.EndUtc == endUtc)
            || (r.TimeBlock.JiraKey == item.Key && r.TimeBlock.Status != TimeBlockStatus.Submitted));

        RunEditDialog(EditDialogViewModel
            .ForCatsRange(_selectedDate, item.StartLocal, item.EndLocal, item.Key,
                existing?.TimeBlock.Note, canDelete: existing is not null, LoadTicketSuggestions())
            .WithInitialRange(startUtc, endUtc));
    }

    private void EditGap(TimelineGapItem item) =>
        RunEditDialog(EditDialogViewModel
            .ForGap(_selectedDate, item.StartLocal, item.EndLocal, LoadTicketSuggestions())
            .WithInitialRange(item.StartLocal.ToUniversalTime(), item.EndLocal.ToUniversalTime()));

    private void EditLine(CatsLineViewModel line) =>
        RunEditDialog(EditDialogViewModel.ForCatsLine(line.Id, line.Block, LoadTicketSuggestions()));

    private IReadOnlyList<TicketSuggestion> LoadTicketSuggestions() =>
        _jiraTicketRepository?.GetAll()
            .Select(r => new TicketSuggestion(
                r.Ticket.Key, r.Ticket.Summary ?? string.Empty, r.Ticket.Status ?? string.Empty,
                r.Ticket.Posid, r.Ticket.Zwpid))
            .ToList() ?? [];

    private void RunEditDialog(EditDialogViewModel dialog)
    {
        if (_timeBlockRepository is null || ShowEditDialog?.Invoke(dialog) != true)
        {
            return;
        }

        if (dialog.Outcome == EditDialogOutcome.Saved)
        {
            ApplySave(dialog);
        }
        else if (dialog.Outcome == EditDialogOutcome.Deleted)
        {
            ApplyDelete(dialog);
        }

        LoadDay(_selectedDate);
    }

    private void ApplySave(EditDialogViewModel dialog)
    {
        switch (dialog.Kind)
        {
            // Édition d'une ligne : mise à jour en place, statut « Modifié ».
            case EditDialogKind.CatsLine:
                _timeBlockRepository!.Update(dialog.LineId!.Value, dialog.InitialLine! with
                {
                    JiraKey = dialog.SelectedKey,
                    Posid = dialog.SelectedPosid,
                    Zwpid = dialog.SelectedZwpid,
                    Note = dialog.Note,
                    DurationHours = dialog.DurationHours,
                    Status = TimeBlockStatus.Edited,
                });
                break;

            // Imputation d'une zone ou d'un segment : nouvelle plage manuelle, qui est aussi la ligne créée
            // (le modèle actuel ne sépare pas plages et lignes : 1 time_block = 1 plage-ligne).
            case EditDialogKind.ImputeGap:
            case EditDialogKind.CapturedActivity:
                if (dialog.SelectedKey is not null)
                {
                    _timeBlockRepository!.Insert(NewRange(dialog));
                }

                break;

            case EditDialogKind.CatsRange:
                SaveRange(dialog);
                break;
        }
    }

    private void SaveRange(EditDialogViewModel dialog)
    {
        var rows = _timeBlockRepository!.GetByDateRange(dialog.Date, dialog.Date);

        // Plage manuelle (créée par une imputation antérieure) : réédition directe, appariée par bornes.
        var byBounds = rows.FirstOrDefault(r =>
            r.TimeBlock.StartUtc == dialog.InitialStartUtc && r.TimeBlock.EndUtc == dialog.InitialEndUtc);
        if (byBounds is not null)
        {
            _timeBlockRepository.Update(byBounds.Id, byBounds.TimeBlock with
            {
                StartUtc = dialog.StartUtc,
                EndUtc = dialog.EndUtc,
                JiraKey = dialog.SelectedKey,
                Posid = dialog.SelectedPosid,
                Zwpid = dialog.SelectedZwpid,
                Note = dialog.Note,
                DurationHours = dialog.RangeHours,
                Status = TimeBlockStatus.Edited,
            });
            return;
        }

        if (dialog.SelectedKey is null)
        {
            return;
        }

        // Plage issue du corrélateur (recalculée à chaque chargement) : la répercussion persistable vit sur
        // la ligne du ticket — ajustée si elle existe, créée sinon.
        var line = rows.FirstOrDefault(r =>
            r.TimeBlock.JiraKey == dialog.SelectedKey && r.TimeBlock.Status != TimeBlockStatus.Submitted);
        if (line is not null)
        {
            var delta = dialog.RangeHours
                - (dialog.SelectedKey == dialog.InitialJiraKey ? dialog.InitialRangeHours : 0);
            _timeBlockRepository.Update(line.Id, line.TimeBlock with
            {
                Posid = dialog.SelectedPosid,
                Zwpid = dialog.SelectedZwpid,
                Note = dialog.Note,
                DurationHours = Math.Max(0, line.TimeBlock.DurationHours + delta),
                Status = TimeBlockStatus.Edited,
            });
        }
        else
        {
            _timeBlockRepository.Insert(NewRange(dialog));
        }
    }

    private void ApplyDelete(EditDialogViewModel dialog)
    {
        switch (dialog.Kind)
        {
            case EditDialogKind.CatsLine:
                _timeBlockRepository!.Delete(dialog.LineId!.Value);
                break;

            case EditDialogKind.CatsRange:
                var rows = _timeBlockRepository!.GetByDateRange(dialog.Date, dialog.Date);
                var byBounds = rows.FirstOrDefault(r =>
                    r.TimeBlock.StartUtc == dialog.InitialStartUtc && r.TimeBlock.EndUtc == dialog.InitialEndUtc);
                if (byBounds is not null)
                {
                    _timeBlockRepository.Delete(byBounds.Id);
                    break;
                }

                var line = rows.FirstOrDefault(r =>
                    r.TimeBlock.JiraKey == dialog.InitialJiraKey && r.TimeBlock.Status != TimeBlockStatus.Submitted);
                if (line is not null)
                {
                    var remaining = line.TimeBlock.DurationHours - dialog.InitialRangeHours;
                    if (remaining <= 0.01)
                    {
                        _timeBlockRepository.Delete(line.Id);
                    }
                    else
                    {
                        _timeBlockRepository.Update(line.Id, line.TimeBlock with
                        {
                            DurationHours = remaining,
                            Status = TimeBlockStatus.Edited,
                        });
                    }
                }

                break;
        }
    }

    private static TimeBlock NewRange(EditDialogViewModel dialog) => new(
        dialog.Date,
        dialog.StartUtc,
        dialog.EndUtc,
        "Imputation manuelle",
        dialog.SelectedKey,
        dialog.SelectedPosid,
        dialog.SelectedZwpid,
        dialog.Note,
        dialog.RangeHours,
        TimeBlockStatus.Edited,
        null);

    private void RecomputeLineAggregates()
    {
        var totalHours = Lines.Sum(l => l.DurationHours);
        TotalProposedLabel = FormatHours(totalHours);
        GaugePercent = Math.Min(100, totalHours / ExpectedDailyHours * 100);
        ValidatedLinesCount = Lines.Count(l => l.Status is TimeBlockStatus.Validated or TimeBlockStatus.Submitted);
    }

    private static string FormatHours(double hours)
    {
        var totalMinutes = (int)Math.Round(hours * 60, MidpointRounding.AwayFromZero);
        return string.Create(CultureInfo.InvariantCulture, $"{totalMinutes / 60}:{totalMinutes % 60:00}");
    }
}
