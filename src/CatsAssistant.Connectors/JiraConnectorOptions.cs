namespace CatsAssistant.Connectors;

/// <summary>
/// L'API Cloud v3 exige une authentification Basic (email + token API) ; l'e-mail du compte n'est pas
/// un secret et n'a donc pas sa place dans le coffre — seul le token transite par <see cref="IJiraTokenProvider"/>.
/// </summary>
public sealed record JiraConnectorOptions
{
    public JiraConnectorOptions(Uri baseUrl, string accountEmail)
    {
        BaseUrl = baseUrl;
        AccountEmail = accountEmail;
    }

    private readonly Uri _baseUrl = null!;

    public Uri BaseUrl
    {
        get => _baseUrl;
        init
        {
            if (value is null
                || !value.IsAbsoluteUri
                || !string.Equals(value.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
                || !value.OriginalString.EndsWith('/'))
            {
                throw new ArgumentException(
                    "BaseUrl doit etre une URI HTTPS absolue et se terminer par '/' (ex. https://ulis-uliege.atlassian.net/).", nameof(value));
            }

            _baseUrl = value;
        }
    }

    private readonly string _accountEmail = null!;

    public string AccountEmail
    {
        get => _accountEmail;
        init
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("AccountEmail ne peut pas etre vide.", nameof(value));
            }

            _accountEmail = value;
        }
    }

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
