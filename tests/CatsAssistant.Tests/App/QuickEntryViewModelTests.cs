using CatsAssistant.App.ViewModels;
using CatsAssistant.Connectors;
using CatsAssistant.Store;

namespace CatsAssistant.Tests.App;

/// <summary>
/// Tiroir « Encodage rapide » (issue #20) : validation (ticket obligatoire, durée &gt; 0), note par
/// défaut « KEY - résumé », et insertion d'une ligne manuelle 'edited' via DayViewModel.
/// </summary>
public class QuickEntryViewModelTests
{
    private static readonly JiraTicket Ticket = new(
        "ULISTROIS-3377",
        "Correctif écran pointages",
        "In Progress",
        null,
        "P.ACSICAT01-01-P-0005 ZS042 (hors clients)",
        "P.ACSICAT01-01-P-0005",
        "ZS042",
        null);

    private static QuickEntryViewModel CreateStandalone(
        Action<JiraTicket, double, string>? addLine = null,
        params JiraTicket[] tickets) =>
        new(new FakeJiraTicketRepository(tickets), addLine ?? ((_, _, _) => { }));

    [Fact]
    public void AddCommand_WithoutTicket_CannotExecute()
    {
        var viewModel = CreateStandalone();
        viewModel.ToggleCommand.Execute(null);

        Assert.False(viewModel.AddCommand.CanExecute(null));

        viewModel.Pick(Ticket);

        Assert.True(viewModel.AddCommand.CanExecute(null));
    }

    [Fact]
    public void Query_FiltersSuggestionsOnKeyAndSummary()
    {
        var other = Ticket with { Key = "ULISTROIS-42", Summary = "Autre sujet" };
        var viewModel = CreateStandalone(null, Ticket, other);
        viewModel.ToggleCommand.Execute(null);

        viewModel.Query = "3377";
        Assert.Equal(["ULISTROIS-3377"], viewModel.Suggestions.Select(t => t.Key));

        viewModel.Query = "autre";
        Assert.Equal(["ULISTROIS-42"], viewModel.Suggestions.Select(t => t.Key));

        viewModel.Query = "";
        Assert.Empty(viewModel.Suggestions);
        Assert.False(viewModel.HasSuggestions);
    }

    [Fact]
    public void Pick_SetsQueryDefaultNoteAndCodes()
    {
        var viewModel = CreateStandalone();
        viewModel.ToggleCommand.Execute(null);

        viewModel.Pick(Ticket);

        Assert.Equal("ULISTROIS-3377", viewModel.Query);
        Assert.Equal("ULISTROIS-3377 - Correctif écran pointages", viewModel.Note);
        Assert.Equal("P.ACSICAT01-01-P-0005 · ZS042", viewModel.CodesLabel);
        Assert.Empty(viewModel.Suggestions);
    }

    [Fact]
    public void Pick_DoesNotOverwriteUserNote()
    {
        var viewModel = CreateStandalone();
        viewModel.ToggleCommand.Execute(null);
        viewModel.Note = "Ma note à moi";

        viewModel.Pick(Ticket);

        Assert.Equal("Ma note à moi", viewModel.Note);
    }

    [Fact]
    public void DefaultNote_IsTruncatedTo80Characters()
    {
        var longTicket = Ticket with { Summary = new string('x', 200) };
        var viewModel = CreateStandalone();
        viewModel.ToggleCommand.Execute(null);

        viewModel.Pick(longTicket);

        Assert.Equal(80, viewModel.Note.Length);
    }

    [Fact]
    public void TypingAfterPick_ClearsSelectedTicket()
    {
        var viewModel = CreateStandalone(null, Ticket);
        viewModel.ToggleCommand.Execute(null);
        viewModel.Pick(Ticket);

        viewModel.Query = "ULISTROIS";

        Assert.Null(viewModel.SelectedTicket);
        Assert.False(viewModel.AddCommand.CanExecute(null));
    }

    [Fact]
    public void DurationStepper_StepsByQuarterHourWithFloor()
    {
        var viewModel = CreateStandalone();
        viewModel.ToggleCommand.Execute(null);

        Assert.Equal(1.0, viewModel.DurationHours);
        Assert.Equal("1:00", viewModel.DurationLabel);

        viewModel.IncrementCommand.Execute(null);
        Assert.Equal("1:15", viewModel.DurationLabel);

        for (var i = 0; i < 10; i++)
        {
            viewModel.DecrementCommand.Execute(null);
        }

        Assert.Equal(0.25, viewModel.DurationHours);
        Assert.False(viewModel.DecrementCommand.CanExecute(null));
    }

    [Fact]
    public void Toggle_ResetsFieldsOnReopen()
    {
        var viewModel = CreateStandalone();
        viewModel.ToggleCommand.Execute(null);
        viewModel.Pick(Ticket);
        viewModel.IncrementCommand.Execute(null);

        viewModel.ToggleCommand.Execute(null); // fermer
        viewModel.ToggleCommand.Execute(null); // rouvrir

        Assert.Null(viewModel.SelectedTicket);
        Assert.Equal(string.Empty, viewModel.Query);
        Assert.Equal(string.Empty, viewModel.Note);
        Assert.Equal(1.0, viewModel.DurationHours);
    }

    [Fact]
    public void Add_ThroughDayViewModel_InsertsEditedManualLine()
    {
        var repository = new FakeTimeBlockRepository();
        var viewModel = new DayViewModel(
            timeBlockRepository: repository,
            jiraTicketRepository: new FakeJiraTicketRepository([Ticket]));

        viewModel.QuickEntry.ToggleCommand.Execute(null);
        viewModel.QuickEntry.Pick(Ticket);
        viewModel.QuickEntry.IncrementCommand.Execute(null); // 1:15
        viewModel.QuickEntry.AddCommand.Execute(null);

        var line = Assert.Single(viewModel.Lines);
        Assert.Equal(TimeBlockStatus.Edited, line.Status);
        Assert.Equal("1:15", line.DurationLabel);
        Assert.Equal("ULISTROIS-3377", line.KeyDisplay);
        Assert.Equal("ULISTROIS-3377 - Correctif écran pointages", line.Note);
        Assert.Equal("P.ACSICAT01-01-P-0005", line.Posid);
        Assert.Equal("ZS042", line.Zwpid);
        Assert.Equal("1:15", viewModel.TotalProposedLabel);
        Assert.False(viewModel.QuickEntry.IsOpen);

        var today = DateOnly.FromDateTime(DateTime.Now);
        var stored = Assert.Single(repository.GetByDateRange(today, today));
        Assert.Equal(TimeBlockStatus.Edited, stored.TimeBlock.Status);
        Assert.Equal(1.25, stored.TimeBlock.DurationHours);
        Assert.Equal("Encodage manuel", stored.TimeBlock.SourceSummary);
    }

    private sealed class FakeJiraTicketRepository(IReadOnlyList<JiraTicket> tickets) : IJiraTicketRepository
    {
        public void Upsert(JiraTicket ticket, DateTime lastSyncUtc) => throw new NotSupportedException();

        public JiraTicketRow? GetByKey(string key) =>
            tickets.Where(t => t.Key == key).Select(t => new JiraTicketRow(t, DateTime.UtcNow)).FirstOrDefault();

        public IReadOnlyList<JiraTicketRow> GetAll() =>
            tickets.Select(t => new JiraTicketRow(t, DateTime.UtcNow)).ToList();
    }
}
