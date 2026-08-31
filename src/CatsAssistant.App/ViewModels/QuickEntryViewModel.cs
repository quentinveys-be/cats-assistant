using System.Collections.ObjectModel;
using System.Globalization;
using CatsAssistant.App.Mvvm;
using CatsAssistant.Connectors;
using CatsAssistant.Store;

namespace CatsAssistant.App.ViewModels;

/// <summary>
/// Tiroir « Encodage rapide » du panneau CATS (issue #20) : ajout manuel d'une ligne sans activité
/// capturée. Ticket obligatoire (recherche dans le cache jira_tickets), durée par pas de 0,25 h,
/// note par défaut « KEY - résumé ». L'insertion en base est du ressort du parent (DayViewModel),
/// qui connaît le jour affiché.
/// </summary>
public sealed class QuickEntryViewModel : ObservableObject
{
    private const double StepHours = 0.25;
    private const int NoteMaxLength = 80;
    private const int MaxSuggestions = 8;

    private readonly IJiraTicketRepository? _ticketRepository;
    private readonly Action<JiraTicket, double, string> _addLine;

    private bool _isOpen;
    private double _durationHours = 1.0;
    private string _query = string.Empty;
    private string _note = string.Empty;
    private JiraTicket? _selectedTicket;
    private bool _isPicking;

    public QuickEntryViewModel(IJiraTicketRepository? ticketRepository, Action<JiraTicket, double, string> addLine)
    {
        _ticketRepository = ticketRepository;
        _addLine = addLine;

        ToggleCommand = new RelayCommand(() => IsOpen = !IsOpen);
        IncrementCommand = new RelayCommand(() => DurationHours += StepHours);
        DecrementCommand = new RelayCommand(() => DurationHours -= StepHours, () => DurationHours > StepHours);
        PickTicketCommand = new RelayCommand(parameter => Pick((JiraTicket)parameter!));
        AddCommand = new RelayCommand(Add, () => _selectedTicket is not null && DurationHours > 0);
    }

    public RelayCommand ToggleCommand { get; }

    public RelayCommand IncrementCommand { get; }

    public RelayCommand DecrementCommand { get; }

    public RelayCommand PickTicketCommand { get; }

    public RelayCommand AddCommand { get; }

    public ObservableCollection<JiraTicket> Suggestions { get; } = [];

    public bool IsOpen
    {
        get => _isOpen;
        private set
        {
            if (SetProperty(ref _isOpen, value) && value)
            {
                Reset();
            }
        }
    }

    public double DurationHours
    {
        get => _durationHours;
        private set
        {
            if (SetProperty(ref _durationHours, Math.Max(StepHours, value)))
            {
                OnPropertyChanged(nameof(DurationLabel));
            }
        }
    }

    public string DurationLabel => FormatHours(DurationHours);

    /// <summary>Texte du champ de recherche : taper invalide le ticket choisi et rouvre les suggestions.</summary>
    public string Query
    {
        get => _query;
        set
        {
            if (!SetProperty(ref _query, value) || _isPicking)
            {
                return;
            }

            _selectedTicket = null;
            OnPropertyChanged(nameof(CodesLabel));
            RefreshSuggestions();
        }
    }

    public string Note
    {
        get => _note;
        set => SetProperty(ref _note, value);
    }

    public JiraTicket? SelectedTicket => _selectedTicket;

    public bool HasSuggestions => Suggestions.Count > 0;

    public string CodesLabel => _selectedTicket is null
        ? "POSID · ZWPID du ticket choisi"
        : $"{_selectedTicket.Posid ?? "POSID ?"} · {_selectedTicket.Zwpid ?? "ZWPID ?"}";

    public void Pick(JiraTicket ticket)
    {
        _selectedTicket = ticket;

        _isPicking = true;
        Query = ticket.Key;
        _isPicking = false;

        if (string.IsNullOrWhiteSpace(Note))
        {
            Note = DefaultNote(ticket);
        }

        Suggestions.Clear();
        OnPropertyChanged(nameof(HasSuggestions));
        OnPropertyChanged(nameof(CodesLabel));
    }

    private void Add()
    {
        var note = string.IsNullOrWhiteSpace(Note) ? DefaultNote(_selectedTicket!) : Note.Trim();
        _addLine(_selectedTicket!, DurationHours, note);
        IsOpen = false;
    }

    private void RefreshSuggestions()
    {
        Suggestions.Clear();

        var query = _query.Trim();
        if (query.Length > 0 && _ticketRepository is not null)
        {
            var matches = _ticketRepository.GetAll()
                .Select(row => row.Ticket)
                .Where(t => t.Key.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || (t.Summary?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
                .Take(MaxSuggestions);

            foreach (var ticket in matches)
            {
                Suggestions.Add(ticket);
            }
        }

        OnPropertyChanged(nameof(HasSuggestions));
    }

    private void Reset()
    {
        _selectedTicket = null;
        DurationHours = 1.0;
        Note = string.Empty;

        _isPicking = true;
        Query = string.Empty;
        _isPicking = false;

        Suggestions.Clear();
        OnPropertyChanged(nameof(HasSuggestions));
        OnPropertyChanged(nameof(CodesLabel));
    }

    private static string DefaultNote(JiraTicket ticket)
    {
        var note = string.IsNullOrWhiteSpace(ticket.Summary) ? ticket.Key : $"{ticket.Key} - {ticket.Summary}";
        return note.Length <= NoteMaxLength ? note : note[..NoteMaxLength];
    }

    private static string FormatHours(double hours)
    {
        var totalMinutes = (int)Math.Round(hours * 60, MidpointRounding.AwayFromZero);
        return string.Create(CultureInfo.InvariantCulture, $"{totalMinutes / 60}:{totalMinutes % 60:00}");
    }
}
