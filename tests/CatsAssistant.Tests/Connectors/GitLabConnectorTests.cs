using System.Net;
using System.Net.Http;
using System.Text;
using CatsAssistant.Connectors;

namespace CatsAssistant.Tests.Connectors;

public class GitLabConnectorTests
{
    [Fact]
    public async Task GetBranchesAsync_ReturnsBranchesWithDefaultFlag()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue("/repository/branches", () => JsonResponse("""
            [
              { "name": "master", "default": true },
              { "name": "ULISTROIS-3101-fix-auth", "default": false }
            ]
            """));
        var connector = CreateConnector(handler, "token-abc");

        var branches = await connector.GetBranchesAsync("42");

        Assert.Equal(2, branches.Count);
        Assert.Equal(new GitLabBranch("master", true), branches[0]);
        Assert.Equal(new GitLabBranch("ULISTROIS-3101-fix-auth", false), branches[1]);
    }

    [Fact]
    public async Task GetBranchesAsync_SendsPrivateTokenHeaderAndProjectIdInPath()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue("/repository/branches", () => JsonResponse("[]"));
        var connector = CreateConnector(handler, "token-abc");

        await connector.GetBranchesAsync("42");

        var request = Assert.Single(handler.Requests);
        Assert.Equal("token-abc", Assert.Single(request.Headers.GetValues("PRIVATE-TOKEN")));
        Assert.Contains("projects/42/repository/branches", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task GetBranchesAsync_FollowsXNextPageHeader_AccumulatesAllPages()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue("/repository/branches", () => JsonResponse(
            """[ { "name": "master", "default": true }, { "name": "b2", "default": false } ]""",
            nextPage: 2));
        handler.Enqueue("/repository/branches", () => JsonResponse(
            """[ { "name": "b3", "default": false } ]"""));
        var connector = CreateConnector(handler, "token-abc");

        var branches = await connector.GetBranchesAsync("42");

        Assert.Equal(["master", "b2", "b3"], branches.Select(b => b.Name));
        var branchRequests = handler.Requests.Where(r => r.RequestUri!.AbsolutePath.EndsWith("/repository/branches")).ToList();
        Assert.Equal(2, branchRequests.Count);
        Assert.Contains("page=1", branchRequests[0].RequestUri!.Query);
        Assert.Contains("page=2", branchRequests[1].RequestUri!.Query);
    }

    [Fact]
    public async Task GetCommitsAsync_MessageHasDashFormatKey_ExtractsNormalizedJiraKeyAndSha()
    {
        var handler = new FakeHttpMessageHandler();
        SeedIdentity(handler, "quentin.veys@example.com");
        handler.Enqueue("/repository/commits", () => JsonResponse(
            $"[{CommitJson("c1", "fix(auth): corrige le logon ULISTROIS-3101", "quentin.veys@example.com")}]"));
        var connector = CreateConnector(handler, "token-abc");

        var commits = await connector.GetCommitsAsync("42", "master", DateTimeOffset.UtcNow.AddDays(-7));

        var result = Assert.Single(commits);
        Assert.Equal("c1", result.Sha);
        Assert.Equal("ULISTROIS-3101", result.JiraKey);
        Assert.Equal("42", result.Repo);
        Assert.Equal("master", result.Branch);
    }

    [Fact]
    public async Task GetCommitsAsync_MessageHasSlashFormatKey_ExtractsNormalizedJiraKey()
    {
        var handler = new FakeHttpMessageHandler();
        SeedIdentity(handler, "quentin.veys@example.com");
        handler.Enqueue("/repository/commits", () => JsonResponse(
            $"[{CommitJson("c1", "wip ULISTROIS/3101", "quentin.veys@example.com")}]"));
        var connector = CreateConnector(handler, "token-abc");

        var commits = await connector.GetCommitsAsync("42", "master", DateTimeOffset.UtcNow.AddDays(-7));

        Assert.Equal("ULISTROIS-3101", Assert.Single(commits).JiraKey);
    }

    [Fact]
    public async Task GetCommitsAsync_NoKeyInMessage_FallsBackToBranchName()
    {
        var handler = new FakeHttpMessageHandler();
        SeedIdentity(handler, "quentin.veys@example.com");
        handler.Enqueue("/repository/commits", () => JsonResponse(
            $"[{CommitJson("c1", "wip sans ticket", "quentin.veys@example.com")}]"));
        var connector = CreateConnector(handler, "token-abc");

        var commits = await connector.GetCommitsAsync("42", "ULISTROIS-3101-fix-auth", DateTimeOffset.UtcNow.AddDays(-7));

        Assert.Equal("ULISTROIS-3101", Assert.Single(commits).JiraKey);
    }

    [Fact]
    public async Task GetCommitsAsync_NoKeyAnywhere_ReturnsNullJiraKey()
    {
        var handler = new FakeHttpMessageHandler();
        SeedIdentity(handler, "quentin.veys@example.com");
        handler.Enqueue("/repository/commits", () => JsonResponse(
            $"[{CommitJson("c1", "wip sans ticket", "quentin.veys@example.com")}]"));
        var connector = CreateConnector(handler, "token-abc");

        var commits = await connector.GetCommitsAsync("42", "chore-cleanup", DateTimeOffset.UtcNow.AddDays(-7));

        Assert.Null(Assert.Single(commits).JiraKey);
    }

    [Fact]
    public async Task GetCommitsAsync_CommitFromOtherAuthor_IsFilteredOut()
    {
        var handler = new FakeHttpMessageHandler();
        SeedIdentity(handler, "quentin.veys@example.com");
        var mine = CommitJson("c1", "ULISTROIS-3101 mine", "quentin.veys@example.com");
        var other = CommitJson("c2", "ULISTROIS-3102 not mine", "someone.else@example.com");
        handler.Enqueue("/repository/commits", () => JsonResponse($"[{mine},{other}]"));
        var connector = CreateConnector(handler, "token-abc");

        var commits = await connector.GetCommitsAsync("42", "master", DateTimeOffset.UtcNow.AddDays(-7));

        Assert.Equal("c1", Assert.Single(commits).Sha);
    }

    [Fact]
    public async Task GetCommitsAsync_AuthorMatchesSecondaryConfirmedEmail_IsIncluded()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue("/user", () => JsonResponse("""{ "id": 1, "username": "qveys", "email": "quentin.veys@example.com" }"""));
        handler.Enqueue("/user/emails", () => JsonResponse("""[ { "id": 2, "email": "qveys@users.noreply.example.com" } ]"""));
        handler.Enqueue("/repository/commits", () => JsonResponse(
            $"[{CommitJson("c1", "ULISTROIS-3101 via email secondaire", "qveys@users.noreply.example.com")}]"));
        var connector = CreateConnector(handler, "token-abc");

        var commits = await connector.GetCommitsAsync("42", "master", DateTimeOffset.UtcNow.AddDays(-7));

        Assert.Equal("c1", Assert.Single(commits).Sha);
    }

    [Fact]
    public async Task GetCommitsAsync_FollowsXNextPageHeader_FiltersAcrossAllPages()
    {
        var handler = new FakeHttpMessageHandler();
        SeedIdentity(handler, "quentin.veys@example.com");
        handler.Enqueue("/repository/commits", () => JsonResponse(
            $"[{CommitJson("c1", "ULISTROIS-3101 page 1", "someone.else@example.com")}]",
            nextPage: 2));
        handler.Enqueue("/repository/commits", () => JsonResponse(
            $"[{CommitJson("c2", "ULISTROIS-3102 page 2", "quentin.veys@example.com")}]"));
        var connector = CreateConnector(handler, "token-abc");

        var commits = await connector.GetCommitsAsync("42", "master", DateTimeOffset.UtcNow.AddDays(-30));

        Assert.Equal("c2", Assert.Single(commits).Sha);
        var commitRequests = handler.Requests.Where(r => r.RequestUri!.AbsolutePath.EndsWith("/repository/commits")).ToList();
        Assert.Equal(2, commitRequests.Count);
    }

    [Fact]
    public async Task GetCommitsAsync_NoEmailOnAccount_ThrowsInvalidOperationException()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue("/user", () => JsonResponse("""{ "id": 1, "username": "qveys", "email": null }"""));
        handler.Enqueue("/user/emails", () => JsonResponse("[]"));
        var connector = CreateConnector(handler, "token-abc");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => connector.GetCommitsAsync("42", "master", DateTimeOffset.UtcNow.AddDays(-7)));
    }

    [Fact]
    public async Task GetCommitsAsync_NoTokenAvailable_ThrowsInvalidOperationException()
    {
        var handler = new FakeHttpMessageHandler();
        var connector = CreateConnector(handler, token: null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => connector.GetCommitsAsync("42", "master", DateTimeOffset.UtcNow.AddDays(-7)));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetCommitsAsync_ServerReturnsError_ThrowsHttpRequestException()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue("/user", () => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var connector = CreateConnector(handler, "token-abc");

        await Assert.ThrowsAsync<HttpRequestException>(
            () => connector.GetCommitsAsync("42", "master", DateTimeOffset.UtcNow.AddDays(-7)));
    }

    private static void SeedIdentity(FakeHttpMessageHandler handler, string email)
    {
        handler.Enqueue("/user", () => JsonResponse($$"""{ "id": 1, "username": "qveys", "email": "{{email}}" }"""));
        handler.Enqueue("/user/emails", () => JsonResponse("[]"));
    }

    private static GitLabConnector CreateConnector(FakeHttpMessageHandler handler, string? token)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://gitlab.example.com/api/v4/") };
        return new GitLabConnector(httpClient, new StaticGitLabTokenProvider(token));
    }

    private static string CommitJson(string id, string message, string authorEmail) => $$"""
        { "id": "{{id}}", "created_at": "2026-08-05T10:00:00Z", "message": "{{message}}", "author_email": "{{authorEmail}}" }
        """;

    private static HttpResponseMessage JsonResponse(string json, int? nextPage = null)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

        if (nextPage is not null)
        {
            response.Headers.Add("X-Next-Page", nextPage.Value.ToString());
        }

        return response;
    }

    private sealed class StaticGitLabTokenProvider(string? token) : IGitLabTokenProvider
    {
        public Task<string?> GetTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult(token);
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, Queue<Func<HttpResponseMessage>>> _routes = new();

        public List<HttpRequestMessage> Requests { get; } = [];

        public void Enqueue(string pathSuffix, Func<HttpResponseMessage> respond)
        {
            if (!_routes.TryGetValue(pathSuffix, out var queue))
            {
                queue = new Queue<Func<HttpResponseMessage>>();
                _routes[pathSuffix] = queue;
            }

            queue.Enqueue(respond);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);

            var path = request.RequestUri!.AbsolutePath;
            var match = _routes.Keys.FirstOrDefault(path.EndsWith);
            if (match is null || _routes[match].Count == 0)
            {
                throw new InvalidOperationException($"Aucune réponse simulée pour {path}");
            }

            return Task.FromResult(_routes[match].Dequeue()());
        }
    }
}
