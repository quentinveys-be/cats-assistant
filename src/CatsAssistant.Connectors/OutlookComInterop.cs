using System.Reflection;
using System.Runtime.InteropServices;

namespace CatsAssistant.Connectors;

/// <summary>
/// Minimal late-bound COM call helper. Avoids a dependency on the Outlook Primary Interop Assembly so
/// the app can build and reflect against the automation surface even without Office installed.
/// </summary>
internal static class OutlookComInterop
{
    public static object? InvokeMethod(object target, string name, params object?[] args) =>
        target.GetType().InvokeMember(name, BindingFlags.InvokeMethod, null, target, args);

    public static object? GetProperty(object target, string name) =>
        target.GetType().InvokeMember(name, BindingFlags.GetProperty, null, target, null);

    public static void SetProperty(object target, string name, object? value) =>
        target.GetType().InvokeMember(name, BindingFlags.SetProperty, null, target, new[] { value });

    public static void Release(object? comObject)
    {
        if (comObject is not null && Marshal.IsComObject(comObject))
        {
            Marshal.ReleaseComObject(comObject);
        }
    }
}
