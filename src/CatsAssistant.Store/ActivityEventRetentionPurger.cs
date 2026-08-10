namespace CatsAssistant.Store;

public sealed class ActivityEventRetentionPurger
{
    public static readonly TimeSpan DefaultRetention = TimeSpan.FromDays(90);

    private readonly IActivityEventRepository _repository;
    private readonly TimeSpan _retention;

    public ActivityEventRetentionPurger(IActivityEventRepository repository, TimeSpan? retention = null)
    {
        _repository = repository;
        _retention = retention ?? DefaultRetention;
    }

    public int Purge(DateTime? nowUtc = null)
    {
        var threshold = (nowUtc ?? DateTime.UtcNow) - _retention;
        return _repository.DeleteOlderThan(threshold);
    }
}
