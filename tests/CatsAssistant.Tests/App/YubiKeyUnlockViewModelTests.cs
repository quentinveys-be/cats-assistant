using CatsAssistant.App;
using CatsAssistant.App.ViewModels;
using CatsAssistant.Secrets;
using CatsAssistant.Tests.Secrets;

namespace CatsAssistant.Tests.App;

public class YubiKeyUnlockViewModelTests
{
    [Fact]
    public async Task RetryAsync_YubiKeyPresent_UnlocksAndRequestsClose()
    {
        using var fixture = new Fixture();
        var viewModel = new YubiKeyUnlockViewModel(fixture.Coordinator);
        var closeRaised = false;
        viewModel.RequestClose += (_, _) => closeRaised = true;

        await viewModel.RetryAsync();

        Assert.True(closeRaised);
        Assert.False(viewModel.IsBusy);
        Assert.Null(viewModel.ErrorMessage);
        Assert.Equal(YubiKeyVaultState.Unlocked, fixture.Coordinator.State);
    }

    [Fact]
    public async Task RetryAsync_YubiKeyAbsent_SetsErrorMessageAndStaysOpen()
    {
        using var fixture = new Fixture();
        fixture.YubiKeyClient.Connected = false;
        var viewModel = new YubiKeyUnlockViewModel(fixture.Coordinator);
        var closeRaised = false;
        viewModel.RequestClose += (_, _) => closeRaised = true;

        await viewModel.RetryAsync();

        Assert.False(closeRaised);
        Assert.False(viewModel.IsBusy);
        Assert.NotNull(viewModel.ErrorMessage);
        Assert.Equal(YubiKeyVaultState.Locked, fixture.Coordinator.State);
    }

    [Fact]
    public async Task RetryAsync_AfterFailure_CanSucceedOnSecondAttempt()
    {
        using var fixture = new Fixture();
        fixture.YubiKeyClient.Connected = false;
        var viewModel = new YubiKeyUnlockViewModel(fixture.Coordinator);
        await viewModel.RetryAsync();
        Assert.NotNull(viewModel.ErrorMessage);

        fixture.YubiKeyClient.Connected = true;
        await viewModel.RetryAsync();

        Assert.Null(viewModel.ErrorMessage);
        Assert.Equal(YubiKeyVaultState.Unlocked, fixture.Coordinator.State);
    }

    [Fact]
    public void ContinueWithoutVaultCommand_SetsDegradedAndRequestsClose()
    {
        using var fixture = new Fixture();
        var viewModel = new YubiKeyUnlockViewModel(fixture.Coordinator);
        var closeRaised = false;
        viewModel.RequestClose += (_, _) => closeRaised = true;

        viewModel.ContinueWithoutVaultCommand.Execute(null);

        Assert.True(closeRaised);
        Assert.Equal(YubiKeyVaultState.Degraded, fixture.Coordinator.State);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _challengeFilePath;

        public Fixture()
        {
            _challengeFilePath = Path.Combine(Path.GetTempPath(), $"cats-assistant-yubikey-dialog-tests-{Guid.NewGuid():N}.challenge");
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
