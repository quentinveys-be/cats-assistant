using Microsoft.Win32;

namespace CatsAssistant.App;

/// <summary>Surface testable de <see cref="StartupRegistration"/> — évite qu'un test de ViewModel touche
/// la vraie clé de registre HKCU.</summary>
public interface IStartupRegistration
{
    bool IsEnabled();

    void Enable(string executablePath);

    void Disable();
}

/// <summary>
/// Opt-in "start with Windows" toggle. Writes only to HKCU Run — no admin rights available, never HKLM.
/// Disabled by default: nothing is written unless <see cref="Enable"/> is called.
/// </summary>
public sealed class StartupRegistration : IStartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CatsAssistant";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is not null;
    }

    public void Enable(string executablePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        key.SetValue(ValueName, $"\"{executablePath}\"");
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
