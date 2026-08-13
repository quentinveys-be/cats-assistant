using CatsAssistant.Connectors;

namespace CatsAssistant.Tests.Connectors;

public class JiraConnectorOptionsTests
{
    private static readonly Uri ValidBaseUrl = new("https://ulis-uliege.atlassian.net/");
    private const string ValidEmail = "quentin@example.com";

    [Fact]
    public void Constructor_ValidHttpsUrlAndEmail_Succeeds()
    {
        var options = new JiraConnectorOptions(ValidBaseUrl, ValidEmail);

        Assert.Equal(ValidBaseUrl, options.BaseUrl);
        Assert.Equal(ValidEmail, options.AccountEmail);
    }

    [Fact]
    public void Constructor_HttpBaseUrl_Throws()
    {
        Assert.Throws<ArgumentException>(() => new JiraConnectorOptions(new Uri("http://ulis-uliege.atlassian.net/"), ValidEmail));
    }

    [Fact]
    public void Constructor_BaseUrlWithoutTrailingSlash_Throws()
    {
        Assert.Throws<ArgumentException>(() => new JiraConnectorOptions(new Uri("https://ulis-uliege.atlassian.net"), ValidEmail));
    }

    [Fact]
    public void Constructor_RelativeBaseUrl_Throws()
    {
        Assert.Throws<ArgumentException>(() => new JiraConnectorOptions(new Uri("/rest/api/3/", UriKind.Relative), ValidEmail));
    }

    [Fact]
    public void Constructor_BlankAccountEmail_Throws()
    {
        Assert.Throws<ArgumentException>(() => new JiraConnectorOptions(ValidBaseUrl, "   "));
    }

    [Fact]
    public void WithExpression_InvalidBaseUrl_ThrowsInsteadOfBypassingValidation()
    {
        var options = new JiraConnectorOptions(ValidBaseUrl, ValidEmail);

        Assert.Throws<ArgumentException>(() => options with { BaseUrl = new Uri("http://evil.example/") });
    }

    [Fact]
    public void Constructor_ZeroOrNegativeMaxResultsPerPage_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new JiraConnectorOptions(ValidBaseUrl, ValidEmail) { MaxResultsPerPage = 0 });
    }
}
