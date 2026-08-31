using CatsAssistant.App.Timeline;
using CatsAssistant.Store;

namespace CatsAssistant.Tests.App.Timeline;

public class IncompleteDaysCounterTests
{
    // Mercredi : les 5 jours ouvrés précédents (lookback=7) sont mar/lun/ven/jeu/mer de la semaine
    // précédente ; le week-end (sam/dim) est exclu.
    private static readonly DateOnly Today = new(2026, 8, 12);

    [Fact]
    public void CountIncompleteWeekdays_ExcludesWeekends()
    {
        var repository = new FakeTimeBlockRepository();

        var count = IncompleteDaysCounter.CountIncompleteWeekdays(repository, Today, lookbackDays: 7);

        // 7 jours en arrière depuis un mercredi : 5 jours ouvrés (aucune ligne -> tous incomplets).
        Assert.Equal(5, count);
    }

    [Fact]
    public void CountIncompleteWeekdays_DayWithEnoughHours_IsNotCounted()
    {
        var repository = new FakeTimeBlockRepository();
        var completeDay = Today.AddDays(-1);
        repository.Add(completeDay, IncompleteDaysCounter.ExpectedDailyHours);

        var count = IncompleteDaysCounter.CountIncompleteWeekdays(repository, Today, lookbackDays: 1);

        Assert.Equal(0, count);
    }

    [Fact]
    public void CountIncompleteWeekdays_DayBelowExpectedHours_IsCounted()
    {
        var repository = new FakeTimeBlockRepository();
        var shortDay = Today.AddDays(-1);
        repository.Add(shortDay, IncompleteDaysCounter.ExpectedDailyHours - 0.25);

        var count = IncompleteDaysCounter.CountIncompleteWeekdays(repository, Today, lookbackDays: 1);

        Assert.Equal(1, count);
    }

    private sealed class FakeTimeBlockRepository : ITimeBlockRepository
    {
        private readonly List<TimeBlockRow> _rows = [];

        public void Add(DateOnly date, double durationHours) => _rows.Add(new TimeBlockRow(
            _rows.Count + 1,
            new TimeBlock(date, date.ToDateTime(TimeOnly.MinValue), date.ToDateTime(TimeOnly.MinValue).AddHours(durationHours),
                "src", "ULISTROIS-1", "POSID", "ZWPID", "note", durationHours, TimeBlockStatus.Proposed, null)));

        public long Insert(TimeBlock timeBlock) => throw new NotSupportedException();

        public void Update(long id, TimeBlock timeBlock) => throw new NotSupportedException();

        public TimeBlockRow? GetById(long id) => throw new NotSupportedException();

        public IReadOnlyList<TimeBlockRow> GetByDateRange(DateOnly fromDate, DateOnly toDate) =>
            _rows.Where(r => r.TimeBlock.Date >= fromDate && r.TimeBlock.Date <= toDate).ToList();

        public int CountUnsubmitted() => throw new NotSupportedException();

        public int DeleteUnsubmitted() => throw new NotSupportedException();
    }
}
