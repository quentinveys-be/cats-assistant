using CatsAssistant.Store;

namespace CatsAssistant.Tests.App;

/// <summary>En mémoire (CLAUDE.md, pas de base réelle en test) : suffisant pour couvrir GetByDateRange/Update.</summary>
internal sealed class FakeTimeBlockRepository : ITimeBlockRepository
{
    private readonly Dictionary<long, TimeBlock> _blocks = [];
    private long _nextId = 1;

    public long Insert(TimeBlock timeBlock)
    {
        var id = _nextId++;
        _blocks[id] = timeBlock;
        return id;
    }

    public void Update(long id, TimeBlock timeBlock) => _blocks[id] = timeBlock;

    public void Delete(long id) => _blocks.Remove(id);

    public TimeBlockRow? GetById(long id) => _blocks.TryGetValue(id, out var block) ? new TimeBlockRow(id, block) : null;

    public IReadOnlyList<TimeBlockRow> GetByDateRange(DateOnly fromDate, DateOnly toDate) =>
        _blocks
            .Where(kvp => kvp.Value.Date >= fromDate && kvp.Value.Date <= toDate)
            .Select(kvp => new TimeBlockRow(kvp.Key, kvp.Value))
            .OrderBy(row => row.TimeBlock.StartUtc)
            .ToList();

    public int CountUnsubmitted() => _blocks.Count(kvp => kvp.Value.Status != TimeBlockStatus.Submitted);

    public int DeleteUnsubmitted()
    {
        var ids = _blocks.Where(kvp => kvp.Value.Status != TimeBlockStatus.Submitted).Select(kvp => kvp.Key).ToList();
        foreach (var id in ids) _blocks.Remove(id);
        return ids.Count;
    }
}
