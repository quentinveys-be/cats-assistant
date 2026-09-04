using System.Net;
using CatsAssistant.App.Views;
using CatsAssistant.Filler;

namespace CatsAssistant.App.Services;

/// <summary>
/// Implémentation WPF d'<see cref="ISapSessionProvider"/> (issue #27) : délègue le logon à une
/// <see cref="SapLogonWindow"/>. Le délégué de logon est injectable pour les tests, qui n'ouvrent jamais de
/// fenêtre WebView2 réelle.
/// </summary>
public sealed class SapSessionProvider : ISapSessionProvider
{
    private readonly Func<CancellationToken, Task<CookieContainer?>> _logon;

    public SapSessionProvider()
        : this(ct => new SapLogonWindow().LogonAsync(ct))
    {
    }

    public SapSessionProvider(Func<CancellationToken, Task<CookieContainer?>> logon)
    {
        _logon = logon;
    }

    public SapSessionState State { get; private set; } = SapSessionState.NotConnected;

    public CookieContainer? Cookies { get; private set; }

    public event EventHandler? StateChanged;

    public async Task<bool> EnsureLogonAsync(CancellationToken cancellationToken = default)
    {
        if (State == SapSessionState.Connected)
        {
            return true;
        }

        var cookies = await _logon(cancellationToken);
        if (cookies is null)
        {
            return false;
        }

        Cookies = cookies;
        SetState(SapSessionState.Connected);
        return true;
    }

    public void ReportUnauthorized()
    {
        if (State == SapSessionState.NotConnected)
        {
            return;
        }

        Cookies = null;
        SetState(SapSessionState.Expired);
    }

    private void SetState(SapSessionState state)
    {
        if (State == state)
        {
            return;
        }

        State = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
