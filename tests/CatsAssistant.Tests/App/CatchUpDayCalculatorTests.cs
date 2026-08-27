using CatsAssistant.App.Services;
using CatsAssistant.Store;

namespace CatsAssistant.Tests.App;

public class CatchUpDayCalculatorTests
{
    // Reprend les dates de la maquette (docs/design/screens/cats-assistant.dc.html) : jeu 6 (incomplet),
    // ven 7 (prêt), lun 10 (à vérifier), mar 11 (aujourd'hui). Aug 8-9 2026 = week-end.
    private static readonly DateOnly Today = new(2026, 8, 11);
    private const double ExpectedHoursPerDay = 7.6;

    [Fact]
    public void ComputeIncompleteDays_MatchesDesignReferenceScenario()
    {
        var repository = new FakeTimeBlockRepository();
        repository.Insert(Block(new DateOnly(2026, 8, 6), 3.25, "ULISTROIS-1", TimeBlockStatus.Proposed));
        repository.Insert(Block(new DateOnly(2026, 8, 7), 7.6, "ULISTROIS-2", TimeBlockStatus.Proposed));
        repository.Insert(Block(new DateOnly(2026, 8, 10), 6.75, jiraKey: null, TimeBlockStatus.Proposed));
        repository.Insert(Block(Today, 7.5, "ULISTROIS-3", TimeBlockStatus.Submitted));

        var days = CatchUpDayCalculator.ComputeIncompleteDays(repository, Today, ExpectedHoursPerDay);

        Assert.Collection(days,
            d =>
            {
                Assert.Equal(new DateOnly(2026, 8, 6), d.Date);
                Assert.Equal(CatchUpDayStatus.Incomplete, d.Status);
                Assert.Equal("4:21 d'activité non corrélée", d.Note);
            },
            d =>
            {
                Assert.Equal(new DateOnly(2026, 8, 7), d.Date);
                Assert.Equal(CatchUpDayStatus.ReadyToValidate, d.Status);
            },
            d =>
            {
                Assert.Equal(new DateOnly(2026, 8, 10), d.Date);
                Assert.Equal(CatchUpDayStatus.NeedsReview, d.Status);
                Assert.Equal("1 ligne sans ticket JIRA", d.Note);
            },
            d =>
            {
                Assert.Equal(Today, d.Date);
                Assert.Equal(CatchUpDayStatus.InProgress, d.Status);
                Assert.Equal("Journée en cours · 1 ligne déjà soumise", d.Note);
            });
    }

    [Fact]
    public void ComputeIncompleteDays_StopsAtFirstAlreadyValidatedBusinessDay()
    {
        var repository = new FakeTimeBlockRepository();
        // Chaîne ininterrompue jusqu'à mer 5 (déjà validé) : la remontée doit s'y arrêter et exclure mar 4
        // même si celui-ci a des lignes non validées.
        repository.Insert(Block(new DateOnly(2026, 8, 4), 1.0, "ULISTROIS-0", TimeBlockStatus.Proposed));
        repository.Insert(Block(new DateOnly(2026, 8, 5), 7.6, "ULISTROIS-1", TimeBlockStatus.Validated));
        repository.Insert(Block(new DateOnly(2026, 8, 6), 3.25, "ULISTROIS-2", TimeBlockStatus.Proposed));
        repository.Insert(Block(new DateOnly(2026, 8, 7), 3.25, "ULISTROIS-3", TimeBlockStatus.Proposed));
        repository.Insert(Block(new DateOnly(2026, 8, 10), 3.25, "ULISTROIS-4", TimeBlockStatus.Proposed));

        var days = CatchUpDayCalculator.ComputeIncompleteDays(repository, Today, ExpectedHoursPerDay);

        Assert.DoesNotContain(days, d => d.Date <= new DateOnly(2026, 8, 5));
        Assert.Contains(days, d => d.Date == new DateOnly(2026, 8, 6));
    }

    [Fact]
    public void ComputeIncompleteDays_StopsAtFirstBusinessDayWithNoBlocksAtAll()
    {
        var repository = new FakeTimeBlockRepository();
        // lun 10 (veille de "aujourd'hui") n'a aucune ligne : rien à rattraper avant ce point.
        repository.Insert(Block(new DateOnly(2026, 8, 6), 3.25, "ULISTROIS-1", TimeBlockStatus.Proposed));

        var days = CatchUpDayCalculator.ComputeIncompleteDays(repository, Today, ExpectedHoursPerDay);

        Assert.DoesNotContain(days, d => d.Date == new DateOnly(2026, 8, 6));
    }

    [Fact]
    public void ComputeIncompleteDays_TodayWithoutBlocks_IsOmitted()
    {
        var repository = new FakeTimeBlockRepository();

        var days = CatchUpDayCalculator.ComputeIncompleteDays(repository, Today, ExpectedHoursPerDay);

        Assert.DoesNotContain(days, d => d.Date == Today);
    }

    [Fact]
    public void ValidateDay_SetsProposedAndEditedRowsToValidated_ButLeavesSubmittedUntouched()
    {
        var repository = new FakeTimeBlockRepository();
        var proposedId = repository.Insert(Block(new DateOnly(2026, 8, 7), 4.0, "ULISTROIS-1", TimeBlockStatus.Proposed));
        var submittedId = repository.Insert(Block(new DateOnly(2026, 8, 7), 3.6, "ULISTROIS-2", TimeBlockStatus.Submitted));
        var blocks = repository.GetByDateRange(new DateOnly(2026, 8, 7), new DateOnly(2026, 8, 7));

        CatchUpDayCalculator.ValidateDay(repository, blocks);

        Assert.Equal(TimeBlockStatus.Validated, repository.GetById(proposedId)!.TimeBlock.Status);
        Assert.Equal(TimeBlockStatus.Submitted, repository.GetById(submittedId)!.TimeBlock.Status);
    }

    [Theory]
    [InlineData(7.6, "7:36")]
    [InlineData(0, "0:00")]
    [InlineData(4.35, "4:21")]
    public void FormatHours_FormatsAsHoursColonMinutes(double hours, string expected) =>
        Assert.Equal(expected, CatchUpDayCalculator.FormatHours(hours));

    private static TimeBlock Block(DateOnly date, double durationHours, string? jiraKey, TimeBlockStatus status) =>
        new(date, date.ToDateTime(TimeOnly.MinValue), date.ToDateTime(TimeOnly.MinValue).AddHours(durationHours),
            "Résumé", jiraKey, "POSID", "ZWPID", string.Empty, durationHours, status, null);
}
