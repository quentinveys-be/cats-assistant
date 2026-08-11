using CatsAssistant.Connectors;

namespace CatsAssistant.Tests.Connectors;

public class JiraKeyNormalizerTests
{
    [Fact]
    public void TryNormalize_SlashFormat_NormalizesToDashFormat()
    {
        var success = JiraKeyNormalizer.TryNormalize("ULISTROIS/3101", out var normalizedKey);

        Assert.True(success);
        Assert.Equal("ULISTROIS-3101", normalizedKey);
    }

    [Fact]
    public void TryNormalize_DashFormat_ReturnsSameNormalizedKey()
    {
        var success = JiraKeyNormalizer.TryNormalize("ULISTROIS-3101", out var normalizedKey);

        Assert.True(success);
        Assert.Equal("ULISTROIS-3101", normalizedKey);
    }

    [Fact]
    public void TryNormalize_KeyEmbeddedInWindowTitle_ExtractsAndNormalizes()
    {
        var success = JiraKeyNormalizer.TryNormalize("ULISTROIS/3101 - IntelliJ IDEA", out var normalizedKey);

        Assert.True(success);
        Assert.Equal("ULISTROIS-3101", normalizedKey);
    }

    [Fact]
    public void TryNormalize_KeyEmbeddedInCommitMessage_ExtractsAndNormalizes()
    {
        var success = JiraKeyNormalizer.TryNormalize("fix(auth): corrige le logon ULISTROIS-3101", out var normalizedKey);

        Assert.True(success);
        Assert.Equal("ULISTROIS-3101", normalizedKey);
    }

    [Fact]
    public void TryNormalize_NoMatch_ReturnsFalseAndEmptyKey()
    {
        var success = JiraKeyNormalizer.TryNormalize("aucun ticket ici", out var normalizedKey);

        Assert.False(success);
        Assert.Equal(string.Empty, normalizedKey);
    }

    [Fact]
    public void TryNormalize_NullInput_ReturnsFalse()
    {
        var success = JiraKeyNormalizer.TryNormalize(null, out var normalizedKey);

        Assert.False(success);
        Assert.Equal(string.Empty, normalizedKey);
    }

    [Fact]
    public void TryNormalize_EmptyInput_ReturnsFalse()
    {
        var success = JiraKeyNormalizer.TryNormalize(string.Empty, out var normalizedKey);

        Assert.False(success);
        Assert.Equal(string.Empty, normalizedKey);
    }
}
