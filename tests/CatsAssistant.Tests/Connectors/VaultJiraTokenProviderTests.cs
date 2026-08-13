using CatsAssistant.Connectors;
using CatsAssistant.Secrets;

namespace CatsAssistant.Tests.Connectors;

public class VaultJiraTokenProviderTests
{
    [Fact]
    public async Task GetTokenAsync_SecretPresent_ReturnsToken()
    {
        var vault = new FakeSecretVault { TokenToReturn = "api-token-123" };
        var provider = new VaultJiraTokenProvider(vault);

        var token = await provider.GetTokenAsync();

        Assert.Equal("api-token-123", token);
        Assert.Equal(SecretName.JiraApiToken, vault.LastReadName);
    }

    [Fact]
    public async Task GetTokenAsync_NoSecretStored_ReturnsNull()
    {
        var vault = new FakeSecretVault { TokenToReturn = null };
        var provider = new VaultJiraTokenProvider(vault);

        var token = await provider.GetTokenAsync();

        Assert.Null(token);
    }

    [Fact]
    public async Task GetTokenAsync_YubiKeyAbsent_DegradesToNullWithoutThrowing()
    {
        var vault = new FakeSecretVault { ThrowOnRead = new YubiKeyNotPresentException() };
        var provider = new VaultJiraTokenProvider(vault);

        var token = await provider.GetTokenAsync();

        Assert.Null(token);
    }

    [Fact]
    public async Task GetTokenAsync_VaultCorrupted_PropagatesException()
    {
        var vault = new FakeSecretVault
        {
            ThrowOnRead = new SecretVaultException("corrompu", new InvalidOperationException()),
        };
        var provider = new VaultJiraTokenProvider(vault);

        await Assert.ThrowsAsync<SecretVaultException>(() => provider.GetTokenAsync());
    }

    private sealed class FakeSecretVault : ISecretVault
    {
        public string? TokenToReturn { get; init; }

        public Exception? ThrowOnRead { get; init; }

        public SecretName? LastReadName { get; private set; }

        public bool IsYubiKeyPresent => true;

        public string? TryRead(SecretName name)
        {
            LastReadName = name;
            if (ThrowOnRead is not null)
            {
                throw ThrowOnRead;
            }

            return TokenToReturn;
        }

        public void Store(SecretName name, string secretValue) => throw new NotSupportedException();

        public void Delete(SecretName name) => throw new NotSupportedException();
    }
}
