namespace CatsAssistant.Collector;

public enum IdleTransition
{
    None,
    BecameIdle,
    BecameActive,
}

public sealed class IdleDetector
{
    public static readonly TimeSpan DefaultThreshold = TimeSpan.FromMinutes(5);

    private readonly TimeSpan _threshold;

    public IdleDetector(TimeSpan? threshold = null)
    {
        _threshold = threshold ?? DefaultThreshold;
    }

    public bool IsIdle { get; private set; }

    public IdleTransition Evaluate(TimeSpan idleDuration)
    {
        if (!IsIdle && idleDuration >= _threshold)
        {
            IsIdle = true;
            return IdleTransition.BecameIdle;
        }

        if (IsIdle && idleDuration < _threshold)
        {
            IsIdle = false;
            return IdleTransition.BecameActive;
        }

        return IdleTransition.None;
    }
}
