using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CatsAssistant.Connectors;

/// <summary>
/// Client REST JIRA Cloud v3 (ADR D7) : GET /rest/api/3/search/jql, auth Basic (email + token du coffre),
/// pagination par nextPageToken. Ne persiste rien — la persistance dans jira_tickets est l'étape 2.5.
/// </summary>
public sealed class JiraCloudConnector : IJiraConnector
{
    private const string SearchEndpoint = "rest/api/3/search/jql";
    private const string FieldsParameter = "summary,status,customfield_10044,customfield_10045,customfield_10046";

    private readonly HttpClient _httpClient;
    private readonly IJiraTokenProvider _tokenProvider;
    private readonly JiraConnectorOptions _options;

    public JiraCloudConnector(HttpClient httpClient, IJiraTokenProvider tokenProvider, JiraConnectorOptions options)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
        _options = options;
    }

    public async Task<IReadOnlyList<JiraTicket>> FetchAssignedTicketsAsync(CancellationToken cancellationToken = default)
    {
        var basicCredentials = await BuildBasicCredentialsAsync(cancellationToken).ConfigureAwait(false);

        var tickets = new List<JiraTicket>();
        string? nextPageToken = null;

        do
        {
            using var request = BuildRequest(nextPageToken, basicCredentials);
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

            tickets.AddRange(ParseIssues(document.RootElement));
            nextPageToken = ReadNextPageToken(document.RootElement);
        }
        while (!string.IsNullOrEmpty(nextPageToken));

        return tickets;
    }

    private async Task<string> BuildBasicCredentialsAsync(CancellationToken cancellationToken)
    {
        var token = await _tokenProvider.GetTokenAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(token))
        {
            throw new InvalidOperationException("Aucun token JIRA disponible (coffre non configuré ou verrouillé).");
        }

        return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.AccountEmail}:{token}"));
    }

    private HttpRequestMessage BuildRequest(string? nextPageToken, string basicCredentials)
    {
        var query = new List<string>
        {
            $"jql={Uri.EscapeDataString(_options.Jql)}",
            $"fields={Uri.EscapeDataString(FieldsParameter)}",
            $"maxResults={_options.MaxResultsPerPage}",
        };
        if (!string.IsNullOrEmpty(nextPageToken))
        {
            query.Add($"nextPageToken={Uri.EscapeDataString(nextPageToken)}");
        }

        var uri = new Uri(_options.BaseUrl, $"{SearchEndpoint}?{string.Join('&', query)}");

        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicCredentials);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static IEnumerable<JiraTicket> ParseIssues(JsonElement root)
    {
        if (!root.TryGetProperty("issues", out var issues) || issues.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var issue in issues.EnumerateArray())
        {
            yield return ParseIssue(issue);
        }
    }

    private static JiraTicket ParseIssue(JsonElement issue)
    {
        var key = issue.TryGetProperty("key", out var keyProperty) ? keyProperty.GetString() ?? string.Empty : string.Empty;
        var fields = issue.TryGetProperty("fields", out var fieldsElement) ? fieldsElement : default;

        var summary = ReadString(fields, "summary");
        var status = ReadStatusName(fields);
        var context = ReadAdfPlainText(fields, "customfield_10044");
        var imputationCodeRaw = ReadSelectValue(fields, "customfield_10045");
        var effort = ReadNullableDouble(fields, "customfield_10046");
        var extraction = PosidZwpidExtractor.Extract(imputationCodeRaw);

        return new JiraTicket(key, summary, status, context, imputationCodeRaw, extraction.Posid, extraction.Zwpid, effort);
    }

    private static string? ReadString(JsonElement fields, string propertyName)
    {
        if (fields.ValueKind == JsonValueKind.Object
            && fields.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return null;
    }

    private static string? ReadStatusName(JsonElement fields)
    {
        if (fields.ValueKind == JsonValueKind.Object
            && fields.TryGetProperty("status", out var status)
            && status.ValueKind == JsonValueKind.Object
            && status.TryGetProperty("name", out var name)
            && name.ValueKind == JsonValueKind.String)
        {
            return name.GetString();
        }

        return null;
    }

    private static string? ReadAdfPlainText(JsonElement fields, string propertyName)
    {
        if (fields.ValueKind != JsonValueKind.Object || !fields.TryGetProperty(propertyName, out var field)
            || field.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var plainText = AdfDocumentParser.ToPlainText(field.GetRawText());
        return plainText.Length == 0 ? null : plainText;
    }

    private static string? ReadSelectValue(JsonElement fields, string propertyName)
    {
        if (fields.ValueKind == JsonValueKind.Object
            && fields.TryGetProperty(propertyName, out var field)
            && field.ValueKind == JsonValueKind.Object
            && field.TryGetProperty("value", out var value)
            && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return null;
    }

    private static double? ReadNullableDouble(JsonElement fields, string propertyName)
    {
        if (fields.ValueKind == JsonValueKind.Object
            && fields.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.Number)
        {
            return value.GetDouble();
        }

        return null;
    }

    private static string? ReadNextPageToken(JsonElement root)
    {
        if (root.TryGetProperty("nextPageToken", out var token) && token.ValueKind == JsonValueKind.String)
        {
            return token.GetString();
        }

        return null;
    }
}
