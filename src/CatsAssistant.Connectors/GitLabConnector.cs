using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace CatsAssistant.Connectors;

/// <summary>
/// Client REST GitLab (API v4). L'appelant configure <see cref="HttpClient.BaseAddress"/>
/// sur la racine de l'API (ex. https://gitlab.example.com/api/v4/, terminée par un slash).
/// </summary>
public sealed class GitLabConnector : IGitLabConnector
{
    private const int PageSize = 100;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly IGitLabTokenProvider _tokenProvider;
    private IReadOnlySet<string>? _cachedAuthorEmails;

    public GitLabConnector(HttpClient httpClient, IGitLabTokenProvider tokenProvider)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
    }

    public async Task<IReadOnlyList<GitLabBranch>> GetBranchesAsync(
        string projectId,
        CancellationToken cancellationToken = default)
    {
        var dtos = await SendPagedAsync<GitLabBranchDto>(
            $"projects/{Uri.EscapeDataString(projectId)}/repository/branches",
            cancellationToken);

        return dtos.Select(dto => new GitLabBranch(dto.Name, dto.Default)).ToList();
    }

    public async Task<IReadOnlyList<VcsCommit>> GetCommitsAsync(
        string projectId,
        string branch,
        DateTimeOffset sinceUtc,
        CancellationToken cancellationToken = default)
    {
        // Les commits sont filtrés sur l'identité du porteur du token GitLab (tous ses e-mails
        // confirmés, cf. GetCurrentUserEmailsAsync) — un commit auteuré avec un e-mail non
        // enregistré sur le compte GitLab (adresse personnelle jamais ajoutée au profil, par
        // exemple) ne sera pas reconnu comme "de l'utilisateur".
        var authorEmails = await GetCurrentUserEmailsAsync(cancellationToken);

        var requestUri =
            $"projects/{Uri.EscapeDataString(projectId)}/repository/commits" +
            $"?ref_name={Uri.EscapeDataString(branch)}" +
            $"&since={Uri.EscapeDataString(sinceUtc.UtcDateTime.ToString("o"))}";

        var dtos = await SendPagedAsync<GitLabCommitDto>(requestUri, cancellationToken);

        var commits = new List<VcsCommit>();
        foreach (var dto in dtos)
        {
            if (dto.AuthorEmail is null || !authorEmails.Contains(dto.AuthorEmail))
            {
                continue;
            }

            commits.Add(new VcsCommit(dto.Id, dto.CreatedAt, projectId, branch, dto.Message, ExtractJiraKey(dto.Message, branch)));
        }

        return commits;
    }

    private static string? ExtractJiraKey(string message, string branch)
    {
        if (JiraKeyNormalizer.TryNormalize(message, out var keyFromMessage))
        {
            return keyFromMessage;
        }

        return JiraKeyNormalizer.TryNormalize(branch, out var keyFromBranch) ? keyFromBranch : null;
    }

    private async Task<IReadOnlySet<string>> GetCurrentUserEmailsAsync(CancellationToken cancellationToken)
    {
        if (_cachedAuthorEmails is not null)
        {
            return _cachedAuthorEmails;
        }

        var user = await SendAsync<GitLabUserDto>("user", cancellationToken);
        var confirmedEmails = await SendPagedAsync<GitLabEmailDto>("user/emails", cancellationToken);

        var emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var primaryEmail = user?.Email;
        if (!string.IsNullOrWhiteSpace(primaryEmail))
        {
            emails.Add(primaryEmail);
        }

        foreach (var email in confirmedEmails)
        {
            if (!string.IsNullOrWhiteSpace(email.Email))
            {
                emails.Add(email.Email);
            }
        }

        if (emails.Count == 0)
        {
            throw new InvalidOperationException(
                "Aucun e-mail associé au compte GitLab du token : impossible de filtrer les commits par auteur.");
        }

        _cachedAuthorEmails = emails;
        return emails;
    }

    private async Task<T?> SendAsync<T>(string requestUri, CancellationToken cancellationToken)
    {
        var page = await FetchAsync(requestUri, cancellationToken);
        return JsonSerializer.Deserialize<T>(page.Body, JsonOptions);
    }

    /// <summary>
    /// Récupère toutes les pages d'une collection GitLab (pagination par en-tête
    /// <c>X-Next-Page</c>) — nécessaire car l'API renvoie 20 résultats par page par défaut et
    /// n'offre aucune garantie de tri utile pour s'arrêter plus tôt (branche par défaut ou
    /// commit de l'utilisateur pouvant se trouver au-delà de la première page).
    /// </summary>
    private async Task<List<T>> SendPagedAsync<T>(string requestUri, CancellationToken cancellationToken)
    {
        var results = new List<T>();
        var separator = requestUri.Contains('?') ? "&" : "?";
        var page = 1;

        while (true)
        {
            var response = await FetchAsync($"{requestUri}{separator}per_page={PageSize}&page={page}", cancellationToken);
            var items = JsonSerializer.Deserialize<List<T>>(response.Body, JsonOptions);
            results.AddRange(items ?? []);

            if (response.NextPage is null)
            {
                break;
            }

            page = response.NextPage.Value;
        }

        return results;
    }

    private async Task<GitLabPage> FetchAsync(string requestUri, CancellationToken cancellationToken)
    {
        var token = await _tokenProvider.GetTokenAsync(cancellationToken);
        if (string.IsNullOrEmpty(token))
        {
            throw new InvalidOperationException("Aucun token GitLab disponible dans le coffre.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.TryAddWithoutValidation("PRIVATE-TOKEN", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        int? nextPage = null;
        if (response.Headers.TryGetValues("X-Next-Page", out var values) &&
            int.TryParse(values.FirstOrDefault(), out var parsedNextPage))
        {
            nextPage = parsedNextPage;
        }

        var body = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return new GitLabPage(body, nextPage);
    }

    private readonly record struct GitLabPage(byte[] Body, int? NextPage);
}
