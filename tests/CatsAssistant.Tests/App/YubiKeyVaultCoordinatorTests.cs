using CatsAssistant.App;
using CatsAssistant.Secrets;
using CatsAssistant.Tests.Secrets;

namespace CatsAssistant.Tests.App;

public class YubiKeyVaultCoordinatorTests
{
    [Fact]
    public void TryUnlock_YubiKeyPresent_SetsUnlockedAndCachesKey()
    {
        using var fixture = new Fixture();

        var unlocked = fixture.Coordinator.TryUnlock();

        Assert.True(unlocked);
        Assert.Equal(YubiKeyVaultState.Unlocked, fixture.Coordinator.State);
        Assert.False(string.IsNullOrEmpty(fixture.Coordinator.CachedKey));
    }

    [Fact]
    public void TryUnlock_YubiKeyAbsent_StaysLockedAndReturnsFalse()
    {
        using var fixture = new Fixture();
        fixture.YubiKeyClient.Connected = false;

        var unlocked = fixture.Coordinator.TryUnlock();

        Assert.False(unlocked);
        Assert.Equal(YubiKeyVaultState.Locked, fixture.Coordinator.State);
        Assert.Null(fixture.Coordinator.CachedKey);
    }

    [Fact]
    public void TryUnlock_TouchesYubiKeyOnlyOnce_AcrossMultipleCalls()
    {
        using var fixture = new Fixture();

        fixture.Coordinator.TryUnlock();
        fixture.YubiKeyClient.Connected = false; // retirée après coup : ne doit pas invalider un second appel

        Assert.True(fixture.Coordinator.TryUnlock());
    }

    [Fact]
    public void ContinueWithoutVault_SetsDegradedState()
    {
        using var fixture = new Fixture();

        fixture.Coordinator.ContinueWithoutVault();

        Assert.Equal(YubiKeyVaultState.Degraded, fixture.Coordinator.State);
    }

    [Fact]
    public void StateChanged_RaisedOnUnlockTransition()
    {
        using var fixture = new Fixture();
        var raised = 0;
        fixture.Coordinator.StateChanged += (_, _) => raised++;

        fixture.Coordinator.TryUnlock();

        Assert.Equal(1, raised);
    }

    [Fact]
    public void IsYubiKeyPresent_ReflectsClientWithoutTouching()
    {
        using var fixture = new Fixture();
        fixture.YubiKeyClient.Connected = false;

        Assert.False(fixture.Coordinator.IsYubiKeyPresent);
        Assert.Equal(YubiKeyVaultState.Locked, fixture.Coordinator.State);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _challengeFilePath;

        public Fixture()
        {
            _challengeFilePath = Path.Combine(Path.GetTempPath(), $"cats-assistant-vault-coordinator-tests-{Guid.NewGuid():N}.challenge");
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
