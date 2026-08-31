using System.Collections.ObjectModel;
using System.Globalization;
using CatsAssistant.App.Mvvm;
using CatsAssistant.App.Timeline;
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
        Action? navigateToSummary = null)
        : base("Journée")
    {
        _activityEventRepository = activityEventRepository;
        _timeBlockRepository = timeBlockRepository;
        _calendarEventRepository = calendarEventRepository;
        _vcsCommitRepository = vcsCommitRepository;
        _ruleRepository = ruleRepository;
        _correlationEngine = correlationEngine ?? new CorrelationEngine();
        _navigateToSummary = navigateToSummary;

        PreviousDayCommand = new RelayCommand(() => LoadDay(_selectedDate.AddDays(-1)));
        NextDayCommand = new RelayCommand(() => LoadDay(_selectedDate.AddDays(1)));
        TodayCommand = new RelayCommand(() => LoadDay(DateOnly.FromDateTime(DateTime.Now)));
        GoToCatchUpCommand = new RelayCommand(() => navigateToCatchUp?.Invoke(), () => navigateToCatchUp is not null);
        ValidateAllCommand = new RelayCommand(ValidateAll);
        GoToSummaryCommand = new RelayCommand(() => _navigateToSummary?.Invoke());

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

    public ObservableCollection<HourMarkItem> Hours { get; }

    public ObservableCollection<TimelineSegmentItem> Segments { get; }

    public ObservableCollection<TimelineGroupItem> Groups { get; }

    public ObservableCollection<TimelineGapItem> Gaps { get; }

    public ObservableCollection<TimelineMeetingItem> Meetings { get; }

    public ObservableCollection<CatsLineViewModel> Lines { get; }

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
                Lines.Add(new CatsLineViewModel(row, _timeBlockRepository, RecomputeLineAggregates));
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

    private void ValidateAll()
    {
        foreach (var line in Lines)
        {
            line.Validate();
        }
    }

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
