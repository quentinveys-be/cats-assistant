using System.Runtime.ExceptionServices;

namespace CatsAssistant.Connectors;

/// <summary>
/// Runs a delegate guaranteed to execute on an STA thread, marshaling to a dedicated STA thread when the
/// caller isn't already on one. Outlook's COM automation server requires STA and throws
/// RPC_E_WRONG_THREAD (COMException, HRESULT 0x8001010E) when invoked from an MTA/thread-pool thread —
/// which is exactly the context a background sync timer runs on, not the WPF UI thread.
/// </summary>
public static class StaThreadRunner
{
    public static T Run<T>(Func<T> action)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            return action();
        }

        var result = default(T)!;
        ExceptionDispatchInfo? capturedException = null;

        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception ex)
            {
                capturedException = ExceptionDispatchInfo.Capture(ex);
            }
        })
        {
            IsBackground = true,
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        capturedException?.Throw();
        return result;
    }
}
