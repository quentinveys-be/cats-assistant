using System.Windows.Threading;
using CatsAssistant.App;
using CatsAssistant.App.ViewModels;
using CatsAssistant.Secrets;
using CatsAssistant.Tests.Secrets;

namespace CatsAssistant.Tests.App;

public class YubiKeyConnectionCardViewModelTests
{
    [Fact]
    public async Task TestKeyAsync_YubiKeyPresent_ReportsSuccessAndUnlocks()
    {
        using var fixture = new Fixture();
        var viewModel = new YubiKeyConnectionCardViewModel(fixture.Coordinator);

        await viewModel.TestKeyAsync();

        Assert.False(viewModel.IsTesting);
        Assert.Equal("Clé vérifiée avec succès.", viewModel.TestResultMessage);
        Assert.Equal(YubiKeyVaultState.Unlocked, viewModel.Status);
    }

    [Fact]
    public async Task TestKeyAsync_YubiKeyAbsent_ReportsFailureAndStaysLocked()
    {
        using var fixture = new Fixture();
        fixture.YubiKeyClient.Connected = false;
        var viewModel = new YubiKeyConnectionCardViewModel(fixture.Coordinator);

        await viewModel.TestKeyAsync();

        Assert.Equal("Aucune YubiKey détectée.", viewModel.TestResultMessage);
        Assert.Equal(YubiKeyVaultState.Locked, viewModel.Status);
    }

    [Fact]
    public void StateChanged_OnCoordinator_RefreshesStatusOnUiThread()
    {
        using var fixture = new Fixture();
        var viewModel = new YubiKeyConnectionCardViewModel(fixture.Coordinator);

        fixture.Coordinator.TryUnlock();
        PumpPendingDispatcherOperations();

        Assert.Equal(YubiKeyVaultState.Unlocked, viewModel.Status);
        Assert.Equal("déverrouillé", viewModel.StatusLabel);
    }

    private static void PumpPendingDispatcherOperations()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _challengeFilePath;

        public Fixture()
        {
            _challengeFilePath = Path.Combine(Path.GetTempPath(), $"cats-assistant-connection-card-tests-{Guid.NewGuid():N}.challenge");
            YubiKeyClient = new FakeYubiKeyChallengeResponseClient();
            Coordinator = new YubiKeyVaultCoordinator(new BusinessMasterKeyProvider(_challengeFilePath, YubiKeyClient));
        }

        public FakeYubiKeyChallengeResponseClient YubiKeyClient { get; }

        public YubiKeyVaultCoordinator Coordinator { get; }

        public void Dispose()
        {
            if (File.Exists(_challengeFilePath))
            {
                File.Delete(_challengeFilePath);
            }
        }
    }
}
