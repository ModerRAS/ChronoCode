using System.Net;
using System.Text;
using ChronoCode.Services;
using LibGit2Sharp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ChronoCode.Tests;

public class GitServiceTests
{
    [Fact]
    public async Task CreatePullRequestAsync_Throws_WhenGitHubTokenMissing()
    {
        using var repo = new TemporaryRepository("https://github.com/example/project.git");
        var service = CreateService(token: null, _ => new HttpResponseMessage(HttpStatusCode.Created));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePullRequestAsync(repo.Path, "feature/test", "main", "Title", "Body"));
    }

    [Fact]
    public async Task CreatePullRequestAsync_UsesOwnerAndRepoFromSshRemote()
    {
        string? requestUrl = null;
        using var repo = new TemporaryRepository("git@github.com:acme/widgets.git");
        var service = CreateService("test-token", request =>
        {
            requestUrl = request.RequestUri?.ToString();
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("{\"html_url\":\"https://github.com/acme/widgets/pull/123\"}", Encoding.UTF8, "application/json")
            };
        });

        var result = await service.CreatePullRequestAsync(repo.Path, "feature/test", "main", "Title", "Body");

        Assert.Equal("https://github.com/acme/widgets/pull/123", result);
        Assert.Equal("https://api.github.com/repos/acme/widgets/pulls", requestUrl);
    }

    private static GitService CreateService(string? token, Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GitHub:Token"] = token
            })
            .Build();

        var client = new HttpClient(new StubHttpMessageHandler(responder));
        return new GitService(
            NullLogger<GitService>.Instance,
            new StubHttpClientFactory(client),
            configuration);
    }

    private sealed class TemporaryRepository : IDisposable
    {
        public string Path { get; }

        public TemporaryRepository(string remoteUrl)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "chronocode-tests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(Path);
            Repository.Init(Path);

            using var repo = new Repository(Path);
            repo.Network.Remotes.Add("origin", remoteUrl);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public StubHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responder(request));
    }
}
