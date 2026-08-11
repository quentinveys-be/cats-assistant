namespace CatsAssistant.Connectors;

/// <summary>
/// L'API Cloud v3 exige une authentification Basic (email + token API) ; l'e-mail du compte n'est pas
/// un secret et n'a donc pas sa place dans le coffre — seul le token transite par <see cref="IJiraTokenProvider"/>.
/// </summary>
/// <param name="BaseUrl">Doit se terminer par '/' (ex. https://ulis-uliege.atlassian.net/) pour que la
/// résolution relative de l'endpoint de recherche fonctionne.</param>
public sealed record JiraConnectorOptions(Uri BaseUrl, string AccountEmail)
{
    public string Jql { get; init; } = "assignee=currentUser()";

    public int MaxResultsPerPage { get; init; } = 100;
}
