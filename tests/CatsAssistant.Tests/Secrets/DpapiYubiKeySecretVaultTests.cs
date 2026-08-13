using System.Text;
using CatsAssistant.Secrets;

namespace CatsAssistant.Tests.Secrets;

public class DpapiYubiKeySecretVaultTests
{
    [Fact]
    public void StoreThenTryRead_RoundTrips_WhenYubiKeyPresent()
    {
        using var fixture = new VaultFixture();

        fixture.Vault.Store(SecretName.JiraApiToken, "jira-token-value");

        Assert.Equal("jira-token-value", fixture.Vault.TryRead(SecretName.JiraApiToken));
    }

    [Fact]
    public void TryRead_ReturnsNull_WhenNothingStored()
    {
        using var fixture = new VaultFixture();

        Assert.Null(fixture.Vault.TryRead(SecretName.GitLabPersonalToken));
    }

    [Fact]
    public void Store_Throws_WhenYubiKeyAbsent()
    {
        using var fixture = new VaultFixture();
        fixture.YubiKeyClient.Connected = false;

        Assert.Throws<YubiKeyNotPresentException>(
            () => fixture.Vault.Store(SecretName.JiraApiToken, "jira-token-value"));
    }

    [Fact]
    public void TryRead_Throws_WhenYubiKeyAbsentButSecretExists()
    {
        using var fixture = new VaultFixture();
        fixture.Vault.Store(SecretName.GitLabPersonalToken, "gitlab-token-value");
        fixture.YubiKeyClient.Connected = false;

        Assert.Throws<YubiKeyNotPresentException>(() => fixture.Vault.TryRead(SecretName.GitLabPersonalToken));
    }

    [Fact]
    public void TryRead_Throws_SecretVaultException_WhenDifferentYubiKey()
    {
        using var fixture = new VaultFixture();
        fixture.Vault.Store(SecretName.JiraApiToken, "jira-token-value");

        var vaultWithAnotherKey = new DpapiYubiKeySecretVault(fixture.VaultDirectory, new FakeYubiKeyChallengeResponseClient());

        Assert.Throws<SecretVaultException>(() => vaultWithAnotherKey.TryRead(SecretName.JiraApiToken));
    }

    [Fact]
    public void Delete_RemovesSecret()
    {
        using var fixture = new VaultFixture();
        fixture.Vault.Store(SecretName.JiraApiToken, "jira-token-value");

        fixture.Vault.Delete(SecretName.JiraApiToken);

        Assert.Null(fixture.Vault.TryRead(SecretName.JiraApiToken));
    }

    [Fact]
    public void Delete_DoesNotThrow_WhenSecretAbsent()
    {
        using var fixture = new VaultFixture();

        var exception = Record.Exception(() => fixture.Vault.Delete(SecretName.JiraApiToken));

        Assert.Null(exception);
    }

    [Fact]
    public void Store_NeverPersistsSecretInClear()
    {
        using var fixture = new VaultFixture();
        const string secretValue = "super-secret-jira-token-do-not-leak";

        fixture.Vault.Store(SecretName.JiraApiToken, secretValue);

        var storedFile = Directory.GetFiles(fixture.VaultDirectory).Single();
        var storedBytes = File.ReadAllBytes(storedFile);
        var plaintextBytes = Encoding.UTF8.GetBytes(secretValue);

        var containsPlaintext = ChunksOf(storedBytes, plaintextBytes.Length)
            .Any(chunk => chunk.AsSpan().SequenceEqual(plaintextBytes));
        Assert.False(containsPlaintext);
    }

    [Fact]
    public void IsYubiKeyPresent_ReflectsClientState()
    {
        using var fixture = new VaultFixture();

        Assert.True(fixture.Vault.IsYubiKeyPresent);

        fixture.YubiKeyClient.Connected = false;

        Assert.False(fixture.Vault.IsYubiKeyPresent);
    }

    private static IEnumerable<byte[]> ChunksOf(byte[] haystack, int length)
    {
        for (var i = 0; i <= haystack.Length - length; i++)
        {
            yield return haystack[i..(i + length)];
        }
    }

    private sealed class VaultFixture : IDisposable
    {
        public VaultFixture()
        {
            VaultDirectory = Path.Combine(Path.GetTempPath(), $"cats-assistant-secrets-tests-{Guid.NewGuid():N}");
            YubiKeyClient = new FakeYubiKeyChallengeResponseClient();
            Vault = new DpapiYubiKeySecretVault(VaultDirectory, YubiKeyClient);
        }

        public string VaultDirectory { get; }

        public FakeYubiKeyChallengeResponseClient YubiKeyClient { get; }

        public DpapiYubiKeySecretVault Vault { get; }

        public void Dispose()
        {
            if (Directory.Exists(VaultDirectory))
            {
                Directory.Delete(VaultDirectory, recursive: true);
            }
        }
    }
}
