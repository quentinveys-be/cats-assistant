using CatsAssistant.App.Services;
using CatsAssistant.App.ViewModels;
using CatsAssistant.Store;

namespace CatsAssistant.Tests.App;

public class CatchUpViewModelTests
{
    private static readonly DateOnly Today = new(2026, 8, 11);

    [Fact]
    public void Constructor_WithoutRepository_IsEmpty()
    {
        var viewModel = new CatchUpViewModel(repository: null, today: Today);

        Assert.Empty(viewModel.Days);
        Assert.Equal("Aucune journée à rattraper.", viewModel.Subtitle);
        Assert.Equal(0, viewModel.IncompleteDayCount);
    }

    [Fact]
    public void Constructor_BuildsSubtitleFromEarliestIncompleteDay()
    {
        var repository = TwoIncompleteDaysRepository();

        var viewModel = new CatchUpViewModel(repository, today: Today);

        Assert.Equal(2, viewModel.IncompleteDayCount);
        Assert.Contains("2 journées non complétées depuis le", viewModel.Subtitle);
        Assert.Contains("38 h/semaine, 7:36 attendues par jour", viewModel.Subtitle);
    }

    [Fact]
    public void ValidateCommand_OnDay_MarksItValidatedAndDecrementsBadgeCount()
    {
        var repository = TwoIncompleteDaysRepository();
        var viewModel = new CatchUpViewModel(repository, today: Today);
        var firstDay = viewModel.Days[0];

        firstDay.ValidateCommand.Execute(null);

        Assert.Equal(CatchUpDayStatus.Validated, firstDay.Status);
        Assert.Equal(1, viewModel.IncompleteDayCount);
        Assert.False(firstDay.ValidateCommand.CanExecute(null));
    }

    [Fact]
    public void OpenCommand_InvokesCallbackWithDayDate()
    {
        var repository = TwoIncompleteDaysRepository();
        DateOnly? openedDate = null;
        var viewModel = new CatchUpViewModel(repository, openDay: d => openedDate = d, today: Today);

        viewModel.Days[0].OpenCommand.Execute(null);

        Assert.Equal(viewModel.Days[0].Date, openedDate);
    }

    [Fact]
    public void Walkthrough_ValidateAndContinue_AdvancesThroughEachDayThenStops()
    {
        var repository = TwoIncompleteDaysRepository();
        var viewModel = new CatchUpViewModel(repository, today: Today);

        viewModel.StartWalkCommand.Execute(null);
        Assert.True(viewModel.IsWalking);
        Assert.Equal("Journée 1 sur 2", viewModel.WalkLabel);
        Assert.True(viewModel.Days[0].IsCurrentWalkDay);

        viewModel.WalkValidateCommand.Execute(null);
        Assert.Equal(CatchUpDayStatus.Validated, viewModel.Days[0].Status);
        Assert.False(viewModel.Days[0].IsCurrentWalkDay);
        Assert.True(viewModel.IsWalking);
        Assert.Equal("Journée 2 sur 2", viewModel.WalkLabel);

        viewModel.WalkValidateCommand.Execute(null);
        Assert.Equal(CatchUpDayStatus.Validated, viewModel.Days[1].Status);
        Assert.False(viewModel.IsWalking);
        Assert.Equal(0, viewModel.IncompleteDayCount);
    }

    [Fact]
    public void Walkthrough_Skip_AdvancesWithoutValidating()
    {
        var repository = TwoIncompleteDaysRepository();
        var viewModel = new CatchUpViewModel(repository, today: Today);

        viewModel.StartWalkCommand.Execute(null);
        viewModel.WalkSkipCommand.Execute(null);

        Assert.NotEqual(CatchUpDayStatus.Validated, viewModel.Days[0].Status);
        Assert.Equal("Journée 2 sur 2", viewModel.WalkLabel);
        Assert.Equal(2, viewModel.IncompleteDayCount);
    }

    // Chaîne ininterrompue jusqu'à "aujourd'hui" (lun 10 puis ven 7, jeu 6 vide) : sans ligne la veille,
    // la remontée s'arrêterait avant même d'atteindre ces deux journées (cf. CatchUpDayCalculator).
    private static FakeTimeBlockRepository TwoIncompleteDaysRepository()
    {
        var repository = new FakeTimeBlockRepository();
        repository.Insert(Block(new DateOnly(2026, 8, 7), 3.25, TimeBlockStatus.Proposed));
        repository.Insert(Block(new DateOnly(2026, 8, 10), 7.6, TimeBlockStatus.Proposed));
        return repository;
    }

    private static TimeBlock Block(DateOnly date, double durationHours, TimeBlockStatus status) =>
        new(date, date.ToDateTime(TimeOnly.MinValue), date.ToDateTime(TimeOnly.MinValue).AddHours(durationHours),
            "Résumé", "ULISTROIS-1", "POSID", "ZWPID", string.Empty, durationHours, status, null);
}
