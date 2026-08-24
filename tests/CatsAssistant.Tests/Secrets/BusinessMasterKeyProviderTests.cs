using CatsAssistant.Secrets;

namespace CatsAssistant.Tests.Secrets;

public class BusinessMasterKeyProviderTests
{
    [Fact]
    public void TryGetOrDeriveKey_ReturnsKey_WhenYubiKeyPresent()
    {
        using var fixture = new ProviderFixture();

        var key = fixture.Provider.TryGetOrDeriveKey();

        Assert.False(string.IsNullOrEmpty(key));
    }

    [Fact]
    public void TryGetOrDeriveKey_ReturnsNull_WhenYubiKeyAbsent()
    {
        using var fixture = new ProviderFixture();
        fixture.YubiKeyClient.Connected = false;

        Assert.Null(fixture.Provider.TryGetOrDeriveKey());
    }

    [Fact]
    public void TryGetOrDeriveKey_ReopenedWithSameChallenge_ReturnsSameKey()
    {
        using var fixture = new ProviderFixture();
        var firstKey = fixture.Provider.TryGetOrDeriveKey();

        var reopened = new BusinessMasterKeyProvider(fixture.ChallengeFilePath, fixture.YubiKeyClient);

        Assert.Equal(firstKey, reopened.TryGetOrDeriveKey());
    }

    [Fact]
    public void TryGetOrDeriveKey_CachesKey_NeverTouchesYubiKeyTwice()
    {
        using var fixture = new ProviderFixture();

        fixture.Provider.TryGetOrDeriveKey();
        fixture.YubiKeyClient.Connected = false; // une YubiKey retirée après coup ne doit pas invalider le cache

        Assert.NotNull(fixture.Provider.TryGetOrDeriveKey());
    }

    [Fact]
    public void TryGetOrDeriveKey_ReturnsNull_WhenChallengeFileIsUnreadable()
    {
        var challengeFilePath = Path.Combine(Path.GetTempPath(), $"cats-assistant-business-key-tests-{Guid.NewGuid():N}.challenge");
        Directory.CreateDirectory(challengeFilePath); // un dossier au chemin attendu rend la lecture/écriture impossible
        var provider = new BusinessMasterKeyProvider(challengeFilePath, new FakeYubiKeyChallengeResponseClient());

        try
        {
            Assert.Null(provider.TryGetOrDeriveKey());
        }
        finally
        {
            Directory.Delete(challengeFilePath, recursive: true);
        }
    }

    [Fact]
    public void TryGetOrDeriveKey_ReturnsNull_WhenSdkRejectsChallenge()
    {
        var client = new ThrowingYubiKeyChallengeResponseClient(new ArgumentException("challenge de mauvaise taille"));
        var challengeFilePath = Path.Combine(Path.GetTempPath(), $"cats-assistant-business-key-tests-{Guid.NewGuid():N}.challenge");
        var provider = new BusinessMasterKeyProvider(challengeFilePath, client);

        try
        {
            Assert.Null(provider.TryGetOrDeriveKey());
        }
        finally
        {
            if (File.Exists(challengeFilePath))
            {
                File.Delete(challengeFilePath);
            }
        }
    }

    [Fact]
    public void TryGetOrDeriveKey_ReturnsNull_WhenChallengeFileIsTruncated()
    {
        var challengeFilePath = Path.Combine(Path.GetTempPath(), $"cats-assistant-business-key-tests-{Guid.NewGuid():N}.challenge");
        File.WriteAllBytes(challengeFilePath, new byte[16]); // écriture précédente interrompue : 16 < 64 octets attendus
        var provider = new BusinessMasterKeyProvider(challengeFilePath, new FakeYubiKeyChallengeResponseClient());

        try
        {
            Assert.Null(provider.TryGetOrDeriveKey());
        }
        finally
        {
            File.Delete(challengeFilePath);
        }
    }

    [Fact]
    public void TryGetOrDeriveKey_ReturnsNull_WhenSdkRejectsTouch()
    {
        // Yubico.YubiKey 1.17.2 : GetDataBytes() lève InvalidOperationException si le touch --touch n'est pas
        // fourni à temps ou si l'opération est refusée (Status != Success).
        var client = new ThrowingYubiKeyChallengeResponseClient(new InvalidOperationException("touch refusé"));
        var challengeFilePath = Path.Combine(Path.GetTempPath(), $"cats-assistant-business-key-tests-{Guid.NewGuid():N}.challenge");
        var provider = new BusinessMasterKeyProvider(challengeFilePath, client);

        try
        {
            Assert.Null(provider.TryGetOrDeriveKey());
        }
        finally
        {
            if (File.Exists(challengeFilePath))
            {
                File.Delete(challengeFilePath);
            }
        }
    }

    [Fact]
    public void TryGetOrDeriveKey_ConcurrentFirstRun_AllProvidersConvergeOnSameChallenge()
    {
        // Simule plusieurs instances de l'app démarrant en même temps sur un poste sans business.challenge
        // existant : toutes doivent finir sur la même clé, jamais sur un challenge écrasé en cours de route.
        var challengeFilePath = Path.Combine(Path.GetTempPath(), $"cats-assistant-business-key-tests-{Guid.NewGuid():N}.challenge");
        var sharedYubiKeyClient = new FakeYubiKeyChallengeResponseClient(); // même YubiKey physique pour toutes les instances
        var providers = Enumerable.Range(0, 8)
            .Select(_ => new BusinessMasterKeyProvider(challengeFilePath, sharedYubiKeyClient))
            .ToArray();
        var keys = new string?[providers.Length];

        try
        {
            Parallel.For(0, providers.Length, i => keys[i] = providers[i].TryGetOrDeriveKey());

            Assert.All(keys, key => Assert.Equal(keys[0], key));
            Assert.Equal(64, File.ReadAllBytes(challengeFilePath).Length);
        }
        finally
        {
            foreach (var leftoverTemp in Directory.GetFiles(Path.GetTempPath(), $"{Path.GetFileName(challengeFilePath)}.tmp-*"))
            {
                File.Delete(leftoverTemp);
            }

            if (File.Exists(challengeFilePath))
            {
                File.Delete(challengeFilePath);
            }
        }
    }

    private sealed class ThrowingYubiKeyChallengeResponseClient : IYubiKeyChallengeResponseClient
    {
        private readonly Exception _exception;

        public ThrowingYubiKeyChallengeResponseClient(Exception exception)
        {
            _exception = exception;
        }

        public bool IsPresent() => true;

        public byte[] CalculateHmacSha1Response(byte[] challenge) => throw _exception;
    }

    private sealed class ProviderFixture : IDisposable
    {
        public ProviderFixture()
        {
            ChallengeFilePath = Path.Combine(Path.GetTempPath(), $"cats-assistant-business-key-tests-{Guid.NewGuid():N}.challenge");
            YubiKeyClient = new FakeYubiKeyChallengeResponseClient();
            Provider = new BusinessMasterKeyProvider(ChallengeFilePath, YubiKeyClient);
        }

        public string ChallengeFilePath { get; }

        public FakeYubiKeyChallengeResponseClient YubiKeyClient { get; }

        public BusinessMasterKeyProvider Provider { get; }

        public void Dispose()
        {
            if (File.Exists(ChallengeFilePath))
            {
                File.Delete(ChallengeFilePath);
            }
        }
    }
}
