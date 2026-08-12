namespace CatsAssistant.Connectors;

/// <summary>
/// L'API Cloud v3 exige une authentification Basic (email + token API) ; l'e-mail du compte n'est pas
/// un secret et n'a donc pas sa place dans le coffre — seul le token transite par <see cref="IJiraTokenProvider"/>.
/// </summary>
public sealed record JiraConnectorOptions
{
    public JiraConnectorOptions(Uri baseUrl, string accountEmail)
    {
        if (baseUrl is null || !baseUrl.OriginalString.EndsWith('/'))
        {
            throw new ArgumentException("BaseUrl doit etre non nul et se terminer par '/' (ex. https://ulis-uliege.atlassian.net/).", nameof(baseUrl));
        }

        BaseUrl = baseUrl;
        AccountEmail = accountEmail;
    }

    public Uri BaseUrl { get; init; }

    public string AccountEmail { get; init; }

    public string Jql { get; init; } = "assignee=currentUser()";

    private readonly int _maxResultsPerPage = 100;

    public int MaxResultsPerPage
    {
        get => _maxResultsPerPage;
        init
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "MaxResultsPerPage doit etre strictement positif.");
            }

            _maxResultsPerPage = value;
        }
    }
}
