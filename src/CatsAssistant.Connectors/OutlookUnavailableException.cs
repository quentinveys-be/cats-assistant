namespace CatsAssistant.Connectors;

/// <summary>
/// Raised when the local Outlook profile cannot be reached (not installed, not configured, or COM
/// automation refused/failed).
/// </summary>
public sealed class OutlookUnavailableException : Exception
{
    public OutlookUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
