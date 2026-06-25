using System.Diagnostics;
using System.Net;
using System.Text;
using ChronoCode.Services;
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

    [Fact]
    public async Task CloneRepositoryAsync_Throws_WithGitStderr_WhenCloneFails()
    {
        var service = CreateService(token: null, _ => new HttpResponseMessage(HttpStatusCode.Created));
        var missingRepoPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "chronocode-tests", Guid.NewGuid().ToString("N"), "missing-repo");
        var workspacePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "chronocode-tests", Guid.NewGuid().ToString("N"), "workspace");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CloneRepositoryAsync(missingRepoPath, workspacePath));

        Assert.Contains("git clone", exception.Message);
        var failureText = exception.Message[(exception.Message.IndexOf(": ", StringComparison.Ordinal) + 2)..].Trim();
        Assert.False(string.IsNullOrWhiteSpace(failureText));
    }

    [Fact]
    public async Task CommitChangesAsync_ReturnsEmptyString_WhenRepoClean()
    {
        using var repo = new TemporaryRepository("https://github.com/example/project.git", createInitialCommit: true);
        var service = CreateService(token: null, _ => new HttpResponseMessage(HttpStatusCode.Created));

        var result = await service.CommitChangesAsync(repo.Path, "No-op commit");

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task GetChangedFilesAsync_ReturnsPorcelainStatusAndPath_WhenRepoDirty()
    {
        const string fileName = "tracked.txt";
        using var repo = new TemporaryRepository("https://github.com/example/project.git", createInitialCommit: true, initialFileName: fileName);
        var service = CreateService(token: null, _ => new HttpResponseMessage(HttpStatusCode.Created));

        File.WriteAllText(System.IO.Path.Combine(repo.Path, fileName), "updated content");

        var result = await service.GetChangedFilesAsync(repo.Path);

        var changedFile = Assert.Single(result, item => item.Path == fileName);
        Assert.Equal(" M", changedFile.Status);
        Assert.Equal(fileName, changedFile.Path);
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

        public TemporaryRepository(string remoteUrl, bool createInitialCommit = false, string initialFileName = "README.md")
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "chronocode-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);

            RunGit(Path, "init");
            RunGit(Path, "remote", "add", "origin", remoteUrl);

            if (!createInitialCommit)
            {
                return;
            }

            RunGit(Path, "config", "user.name", "Test User");
            RunGit(Path, "config", "user.email", "test@example.com");
            File.WriteAllText(System.IO.Path.Combine(Path, initialFileName), "initial content");
            RunGit(Path, "add", "-A");
            RunGit(Path, "commit", "-m", "initial");
        }

        public void Dispose()
        {
            if (!Directory.Exists(Path))
            {
                return;
            }

            ClearAttributes(Path);
            Directory.Delete(Path, recursive: true);
        }

        private static void RunGit(string workingDirectory, params string[] arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "git.exe" : "git",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo)!;
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.True(
                process.ExitCode == 0,
                $"git {arguments[0]} failed with exit code {process.ExitCode}. stdout: {stdout} stderr: {stderr}");
        }
        private static void ClearAttributes(string directoryPath)
        {
            foreach (var filePath in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(filePath, FileAttributes.Normal);
            }

            foreach (var subDirectoryPath in Directory.EnumerateDirectories(directoryPath, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(subDirectoryPath, FileAttributes.Normal);
            }

            File.SetAttributes(directoryPath, FileAttributes.Normal);
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
