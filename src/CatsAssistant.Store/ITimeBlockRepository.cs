namespace CatsAssistant.Store;

public interface ITimeBlockRepository
{
    long Insert(TimeBlock timeBlock);

    void Update(long id, TimeBlock timeBlock);

    TimeBlockRow? GetById(long id);

    IReadOnlyList<TimeBlockRow> GetByDateRange(DateOnly fromDate, DateOnly toDate);
}
