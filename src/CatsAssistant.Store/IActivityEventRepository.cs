namespace CatsAssistant.Store;

public interface IActivityEventRepository
{
    long Insert(DateTime timestampUtc, ActivityEventKind kind, string? process, string? windowTitle, string? url);

    IReadOnlyList<ActivityEvent> GetByDateRange(DateTime fromUtc, DateTime toUtc);

    void Delete(long id);

    int DeleteOlderThan(DateTime thresholdUtc);
}
