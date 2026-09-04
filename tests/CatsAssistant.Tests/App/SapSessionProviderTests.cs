using System.Net;
using CatsAssistant.App.Services;
using CatsAssistant.Filler;

namespace CatsAssistant.Tests.App;

public class SapSessionProviderTests
{
    [Fact]
    public void Constructor_InitialState_NotConnected()
    {
        var provider = new SapSessionProvider((_) => Task.FromResult<CookieContainer?>(null));

        Assert.Equal(SapSessionState.NotConnected, provider.State);
        Assert.Null(provider.Cookies);
    }

    [Fact]
    public async Task EnsureLogonAsync_LogonSucceeds_StoresCookiesAndRaisesStateChanged()
    {
        var cookies = new CookieContainer();
        var provider = new SapSessionProvider((_) => Task.FromResult<CookieContainer?>(cookies));
        var raised = 0;
        provider.StateChanged += (_, _) => raised++;

        var result = await provider.EnsureLogonAsync();

        Assert.True(result);
        Assert.Equal(SapSessionState.Connected, provider.State);
        Assert.Same(cookies, provider.Cookies);
        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task EnsureLogonAsync_WindowClosedWithoutCookies_ReturnsFalseAndStaysNotConnected()
    {
        var provider = new SapSessionProvider((_) => Task.FromResult<CookieContainer?>(null));

        var result = await provider.EnsureLogonAsync();

        Assert.False(result);
        Assert.Equal(SapSessionState.NotConnected, provider.State);
    }

    [Fact]
    public async Task EnsureLogonAsync_AlreadyConnected_ReturnsTrueWithoutReopeningWindow()
    {
        var calls = 0;
        var provider = new SapSessionProvider((_) =>
        {
            calls++;
            return Task.FromResult<CookieContainer?>(new CookieContainer());
        });
        await provider.EnsureLogonAsync();

        var result = await provider.EnsureLogonAsync();

        Assert.True(result);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ReportUnauthorized_AfterConnected_ExpiresSessionAndClearsCookies()
    {
        var provider = new SapSessionProvider((_) => Task.FromResult<CookieContainer?>(new CookieContainer()));
        await provider.EnsureLogonAsync();
        var raised = 0;
        provider.StateChanged += (_, _) => raised++;

        provider.ReportUnauthorized();

        Assert.Equal(SapSessionState.Expired, provider.State);
        Assert.Null(provider.Cookies);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void ReportUnauthorized_WhenNeverConnected_DoesNothing()
    {
        var provider = new SapSessionProvider((_) => Task.FromResult<CookieContainer?>(null));
        var raised = 0;
        provider.StateChanged += (_, _) => raised++;

        provider.ReportUnauthorized();

        Assert.Equal(SapSessionState.NotConnected, provider.State);
        Assert.Equal(0, raised);
    }
}
