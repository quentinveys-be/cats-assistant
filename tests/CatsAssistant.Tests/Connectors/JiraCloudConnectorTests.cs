using System.Net;
using System.Text;
using CatsAssistant.Connectors;

namespace CatsAssistant.Tests.Connectors;

public class JiraCloudConnectorTests
{
    private const string PageOneResponse = """
        {
          "issues": [
            {
              "id": "10001",
              "key": "ULISTROIS-100",
              "fields": {
                "summary": "Mise a jour du connecteur JIRA",
                "status": { "name": "En cours" },
                "customfield_10044": {
                  "type": "doc",
                  "version": 1,
                  "content": [
                    { "type": "paragraph", "content": [ { "type": "text", "text": "Contexte ticket 100" } ] }
                  ]
                },
                "customfield_10045": { "value": "ULIS Dev. Maint. U3 (P.ACSICAT01-01-P-0001 ZS010)" },
                "customfield_10046": 12.5
              }
            },
            {
              "id": "10002",
              "key": "ULISTROIS-101",
              "fields": {
                "summary": "Ticket sans code impute",
                "status": { "name": "A faire" }
              }
            }
          ]
        }
        """;

    private const string PageOneResponseWithNextPageToken = """
        {
          "issues": [
            { "id": "10001", "key": "ULISTROIS-100", "fields": { "summary": "Ticket page 1" } }
          ],
          "nextPageToken": "page-2-token"
        }
        """;

    private const string PageTwoResponseWithHorsClientsTrap = """
        {
          "issues": [
            {
              "id": "10003",
              "key": "ULISTROIS-200",
              "fields": {
                "summary": "Correction regex extraction",
                "status": { "name": "A faire" },
                "customfield_10045": { "value": "ULIS (hors clients) Dev. Maint. U3 (P.ACSICAT01-01-P-0005 ZS042)" },
                "customfield_10046": 3
              }
            }
          ]
        }
        """;

    [Fact]
    public async Task FetchAssignedTicketsAsync_SinglePage_ParsesAllFields()
    {
        var handler = new StubHttpMessageHandler(PageTwoResponseWithHorsClientsTrap);
        var connector = CreateConnector(handler, token: "api-token");

        var tickets = await connector.FetchAssignedTicketsAsync();

        var ticket = Assert.Single(tickets);
        Assert.Equal("ULISTROIS-200", ticket.Key);
        Assert.Equal("Correction regex extraction", ticket.Summary);
        Assert.Equal("A faire", ticket.Status);
        Assert.Equal(3d, ticket.Effort);
    }

    [Fact]
    public async Task FetchAssignedTicketsAsync_HorsClientsTrap_ExtractsLastParenthesizedGroupNotFirst()
    {
        var handler = new StubHttpMessageHandler(PageTwoResponseWithHorsClientsTrap);
        var connector = CreateConnector(handler, token: "api-token");

        var tickets = await connector.FetchAssignedTicketsAsync();

        var ticket = Assert.Single(tickets);
        Assert.Equal("ULIS (hors clients) Dev. Maint. U3 (P.ACSICAT01-01-P-0005 ZS042)", ticket.ImputationCodeRaw);
        Assert.Equal("P.ACSICAT01-01-P-0005", ticket.Posid);
        Assert.Equal("ZS042", ticket.Zwpid);
    }

    [Fact]
    public async Task FetchAssignedTicketsAsync_AdfContextField_IsConvertedToPlainText()
    {
        var handler = new StubHttpMessageHandler(PageOneResponse);
        var connector = CreateConnector(handler, token: "api-token");

        var tickets = await connector.FetchAssignedTicketsAsync();

        var ticket = tickets.Single(t => t.Key == "ULISTROIS-100");
        Assert.Equal("Contexte ticket 100", ticket.Context);
        Assert.Equal("P.ACSICAT01-01-P-0001", ticket.Posid);
        Assert.Equal("ZS010", ticket.Zwpid);
        Assert.Equal(12.5, ticket.Effort);
    }

    [Fact]
    public async Task FetchAssignedTicketsAsync_MissingImputationCode_ReturnsNullPosidZwpidWithoutThrowing()
    {
        var handler = new StubHttpMessageHandler(PageOneResponse);
        var connector = CreateConnector(handler, token: "api-token");

        var tickets = await connector.FetchAssignedTicketsAsync();

        var ticket = tickets.Single(t => t.Key == "ULISTROIS-101");
        Assert.Null(ticket.ImputationCodeRaw);
        Assert.Null(ticket.Posid);
        Assert.Null(ticket.Zwpid);
        Assert.Null(ticket.Context);
        Assert.Null(ticket.Effort);
    }

    [Fact]
    public async Task FetchAssignedTicketsAsync_MultiplePages_FollowsNextPageTokenUntilLastPage()
    {
        var handler = new StubHttpMessageHandler(PageOneResponseWithNextPageToken, PageTwoResponseWithHorsClientsTrap);
        var connector = CreateConnector(handler, token: "api-token");

        var tickets = await connector.FetchAssignedTicketsAsync();

        Assert.Equal(2, tickets.Count);
        Assert.Equal(2, handler.Requests.Count);
        Assert.DoesNotContain("nextPageToken", handler.Requests[0].RequestUri!.Query);
        Assert.Contains("nextPageToken=page-2-token", handler.Requests[1].RequestUri!.Query);
    }

    [Fact]
    public async Task FetchAssignedTicketsAsync_SendsBasicAuthorizationHeaderWithEmailAndToken()
    {
        var handler = new StubHttpMessageHandler(PageTwoResponseWithHorsClientsTrap);
        var connector = CreateConnector(handler, token: "api-token", accountEmail: "quentin@example.com");

        await connector.FetchAssignedTicketsAsync();

        var authorization = handler.Requests[0].Headers.Authorization;
        Assert.Equal("Basic", authorization!.Scheme);
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(authorization.Parameter!));
        Assert.Equal("quentin@example.com:api-token", decoded);
    }

    [Fact]
    public async Task FetchAssignedTicketsAsync_NoTokenAvailable_ThrowsWithoutCallingNetwork()
    {
        var handler = new StubHttpMessageHandler(PageTwoResponseWithHorsClientsTrap);
        var connector = CreateConnector(handler, token: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => connector.FetchAssignedTicketsAsync());

        Assert.Empty(handler.Requests);
    }

    private static JiraCloudConnector CreateConnector(
        StubHttpMessageHandler handler,
        string? token,
        string accountEmail = "quentin@example.com")
    {
        var httpClient = new HttpClient(handler);
        var options = new JiraConnectorOptions(new Uri("https://ulis-uliege.atlassian.net/"), accountEmail);
        return new JiraCloudConnector(httpClient, new StubJiraTokenProvider(token), options);
    }

    private sealed class StubJiraTokenProvider : IJiraTokenProvider
    {
        private readonly string? _token;

        public StubJiraTokenProvider(string? token) => _token = token;

        public Task<string?> GetTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult(_token);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<string> _responses;

        public StubHttpMessageHandler(params string[] jsonResponses)
        {
            _responses = new Queue<string>(jsonResponses);
        }

        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var content = _responses.Dequeue();
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(response);
        }
    }
}
