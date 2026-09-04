using System.Net;
using System.Windows.Threading;
using CatsAssistant.App.ViewModels;
using CatsAssistant.Filler;

namespace CatsAssistant.Tests.App;

public class SapConnectionCardViewModelTests
{
    [Fact]
    public void Constructor_NotConnectedProvider_IsNotConfigured()
    {
        var viewModel = new SapConnectionCardViewModel(new FakeSapSessionProvider());

        Assert.Equal(ConnectionStatus.NotConfigured, viewModel.Status);
        Assert.Equal("non configuré", viewModel.StatusLabel);
    }

    [Fact]
    public async Task ConnectAsync_LogonSucceeds_ReflectsConnectedStatus()
    {
        var provider = new FakeSapSessionProvider { LogonResult = true };
        var viewModel = new SapConnectionCardViewModel(provider);

        await viewModel.ConnectAsync();

        Assert.Equal(ConnectionStatus.Connected, viewModel.Status);
        Assert.Equal("connecté", viewModel.StatusLabel);
        Assert.False(viewModel.IsConnecting);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task ConnectAsync_WindowClosedWithoutLogon_SetsErrorMessageAndStaysNotConfigured()
    {
        var provider = new FakeSapSessionProvider { LogonResult = false };
        var viewModel = new SapConnectionCardViewModel(provider);

        await viewModel.ConnectAsync();

        Assert.Equal(ConnectionStatus.NotConfigured, viewModel.Status);
        Assert.NotNull(viewModel.ErrorMessage);
    }

    [Fact]
    public void SessionExpired_OnProvider_RefreshesStatusOnUiThread()
    {
        var provider = new FakeSapSessionProvider { State = SapSessionState.Connected };
        var viewModel = new SapConnectionCardViewModel(provider);

        provider.RaiseExpired();
        PumpPendingDispatcherOperations();

        Assert.Equal(ConnectionStatus.Expired, viewModel.Status);
        Assert.Equal("session expirée — reconnexion nécessaire", viewModel.StatusLabel);
    }

    private static void PumpPendingDispatcherOperations()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private sealed class FakeSapSessionProvider : ISapSessionProvider
    {
        public SapSessionState State { get; set; } = SapSessionState.NotConnected;

        public CookieContainer? Cookies { get; private set; }

        public bool LogonResult { get; set; }

        public event EventHandler? StateChanged;

        public Task<bool> EnsureLogonAsync(CancellationToken cancellationToken = default)
        {
            if (LogonResult)
            {
                Cookies = new CookieContainer();
                State = SapSessionState.Connected;
            }

            return Task.FromResult(LogonResult);
        }

        public void ReportUnauthorized()
        {
            State = SapSessionState.Expired;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseExpired()
        {
            State = SapSessionState.Expired;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
