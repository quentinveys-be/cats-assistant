using CatsAssistant.App.ViewModels;
using CatsAssistant.Secrets;
using CatsAssistant.Store;

namespace CatsAssistant.Tests.App;

public class TokenConnectionCardViewModelTests
{
    [Fact]
    public void Constructor_NoStoredSecret_IsNotConfigured()
    {
        var viewModel = new TokenConnectionCardViewModel(
            "JIRA", "Token personnel", new FakeSecretVault(), new FakeSettingsRepository(),
            SecretName.JiraApiToken, "secrets.jira", tracksExpiry: false);

        Assert.Equal(ConnectionStatus.NotConfigured, viewModel.Status);
        Assert.Equal("", viewModel.MaskedSuffix);
    }

    [Fact]
    public async Task ConfirmReplaceAsync_StoresFullTokenInVaultOnly_NeverInSettings()
    {
        var vault = new FakeSecretVault();
        var settings = new FakeSettingsRepository();
        var viewModel = new TokenConnectionCardViewModel(
            "JIRA", "Token personnel", vault, settings, SecretName.JiraApiToken, "secrets.jira", tracksExpiry: false);

        viewModel.ReplaceCommand.Execute(null);
        viewModel.PendingToken = "abcd1234efgh5678wxyz4f2a";
        await viewModel.ConfirmReplaceAsync();

        Assert.Equal("abcd1234efgh5678wxyz4f2a", vault.Stored[SecretName.JiraApiToken]);
        Assert.All(settings.Values.Values, value => Assert.DoesNotContain("abcd1234efgh5678wxyz4f2a", value));
        Assert.Equal(ConnectionStatus.Connected, viewModel.Status);
        Assert.Equal("••••••••••••4f2a", viewModel.MaskedSuffix);
        Assert.False(viewModel.IsReplacing);
        Assert.Null(viewModel.PendingToken);
    }

    [Fact]
    public async Task ConfirmReplaceAsync_YubiKeyAbsent_SetsErrorAndKeepsFormOpen()
    {
        var vault = new FakeSecretVault { ThrowYubiKeyNotPresent = true };
        var viewModel = new TokenConnectionCardViewModel(
            "JIRA", "Token personnel", vault, new FakeSettingsRepository(), SecretName.JiraApiToken, "secrets.jira", tracksExpiry: false);

        viewModel.ReplaceCommand.Execute(null);
        viewModel.PendingToken = "token-value";
        await viewModel.ConfirmReplaceAsync();

        Assert.True(viewModel.IsReplacing);
        Assert.NotNull(viewModel.ErrorMessage);
        Assert.Equal(ConnectionStatus.NotConfigured, viewModel.Status);
    }

    [Fact]
    public void CancelReplaceCommand_DiscardsPendingToken()
    {
        var viewModel = new TokenConnectionCardViewModel(
            "JIRA", "Token personnel", new FakeSecretVault(), new FakeSettingsRepository(),
            SecretName.JiraApiToken, "secrets.jira", tracksExpiry: false);

        viewModel.ReplaceCommand.Execute(null);
        viewModel.PendingToken = "some-secret";
        viewModel.CancelReplaceCommand.Execute(null);

        Assert.False(viewModel.IsReplacing);
        Assert.Null(viewModel.PendingToken);
        Assert.Equal(ConnectionStatus.NotConfigured, viewModel.Status);
    }

    [Fact]
    public async Task ConfirmReplaceAsync_GitLabTracksExpiry_ReportsExpiredWhenPast()
    {
        var vault = new FakeSecretVault();
        var settings = new FakeSettingsRepository();
        var now = new DateTimeOffset(2026, 8, 27, 0, 0, 0, TimeSpan.Zero);
        var viewModel = new TokenConnectionCardViewModel(
            "GitLab", "Token personnel", vault, settings, SecretName.GitLabPersonalToken, "secrets.gitlab",
            tracksExpiry: true, utcNow: () => now);

        viewModel.ReplaceCommand.Execute(null);
        viewModel.PendingToken = "glpat-xxxxxxxxxxxx1234";
        viewModel.PendingExpiryDate = new DateTime(2026, 1, 1);
        await viewModel.ConfirmReplaceAsync();

        Assert.Equal(ConnectionStatus.Expired, viewModel.Status);
        Assert.Contains("expiré", viewModel.DetailText);
    }

    private sealed class FakeSecretVault : ISecretVault
    {
        public Dictionary<SecretName, string> Stored { get; } = [];

        public bool ThrowYubiKeyNotPresent { get; set; }

        public bool IsYubiKeyPresent => !ThrowYubiKeyNotPresent;

        public void Store(SecretName name, string secretValue)
        {
            if (ThrowYubiKeyNotPresent)
            {
                throw new YubiKeyNotPresentException();
            }

            Stored[name] = secretValue;
        }

        public string? TryRead(SecretName name) => Stored.GetValueOrDefault(name);

        public void Delete(SecretName name) => Stored.Remove(name);
    }

    private sealed class FakeSettingsRepository : ISettingsRepository
    {
        public Dictionary<string, string> Values { get; } = [];

        public string? Get(string key) => Values.GetValueOrDefault(key);

        public void Set(string key, string value) => Values[key] = value;
    }
}
