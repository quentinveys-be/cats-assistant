namespace CatsAssistant.Store;

public interface ITimeBlockRepository
{
    long Insert(TimeBlock timeBlock);

    void Update(long id, TimeBlock timeBlock);

    TimeBlockRow? GetById(long id);

    /// <summary>
    /// Bornes inclusives des deux côtés (contrairement à IVcsCommitRepository/ICalendarEventRepository,
    /// où la borne haute est exclusive) : <paramref name="fromDate"/> et <paramref name="toDate"/> sont
    /// des jours calendaires (DateOnly), pas des instants.
    /// </summary>
    IReadOnlyList<TimeBlockRow> GetByDateRange(DateOnly fromDate, DateOnly toDate);
}
