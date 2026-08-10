using CatsAssistant.Collector;

namespace CatsAssistant.Tests.Collector;

public class RetryBackoffTests
{
    [Fact]
    public void NextDelay_DoublesEachAttempt_UntilCap()
    {
        var backoff = new RetryBackoff(
            initialDelay: TimeSpan.FromSeconds(1),
            maxDelay: TimeSpan.FromSeconds(10));

        Assert.Equal(TimeSpan.FromSeconds(1), backoff.NextDelay());
        Assert.Equal(TimeSpan.FromSeconds(2), backoff.NextDelay());
        Assert.Equal(TimeSpan.FromSeconds(4), backoff.NextDelay());
        Assert.Equal(TimeSpan.FromSeconds(8), backoff.NextDelay());
        Assert.Equal(TimeSpan.FromSeconds(10), backoff.NextDelay());
    }

    [Fact]
    public void Reset_RestartsFromInitialDelay()
    {
        var backoff = new RetryBackoff(initialDelay: TimeSpan.FromSeconds(1), maxDelay: TimeSpan.FromMinutes(1));
        backoff.NextDelay();
        backoff.NextDelay();

        backoff.Reset();

        Assert.Equal(TimeSpan.FromSeconds(1), backoff.NextDelay());
    }

    [Fact]
    public void AttemptCount_TracksNumberOfCalls()
    {
        var backoff = new RetryBackoff();

        backoff.NextDelay();
        backoff.NextDelay();

        Assert.Equal(2, backoff.AttemptCount);
    }
}
