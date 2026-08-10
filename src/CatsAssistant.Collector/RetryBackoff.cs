namespace CatsAssistant.Collector;

public sealed class RetryBackoff
{
    public static readonly TimeSpan DefaultInitialDelay = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan DefaultMaxDelay = TimeSpan.FromMinutes(1);

    private readonly TimeSpan _initialDelay;
    private readonly TimeSpan _maxDelay;

    public RetryBackoff(TimeSpan? initialDelay = null, TimeSpan? maxDelay = null)
    {
        _initialDelay = initialDelay ?? DefaultInitialDelay;
        _maxDelay = maxDelay ?? DefaultMaxDelay;
    }

    public int AttemptCount { get; private set; }

    public TimeSpan NextDelay()
    {
        var delayMs = _initialDelay.TotalMilliseconds * Math.Pow(2, AttemptCount);
        AttemptCount++;

        var delay = TimeSpan.FromMilliseconds(delayMs);
        return delay > _maxDelay ? _maxDelay : delay;
    }

    public void Reset() => AttemptCount = 0;
}
