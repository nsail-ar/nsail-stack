// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSail.Harness.GitHub;
using NSail.Harness.Jobs;
using NSail.Harness.Lanes;
using NSail.Harness.Locks;
using NSail.Harness.Planning;
using NSail.Harness.Repositories;
using NSail.Harness.Watching;

namespace NSail.Harness.Tests;

// GitHub as one idle harness reads it: a board of open stories, some locked by another machine, open
// PRs whose heads fell out of the newest runs. A REST read answers its ETag and 304 when it is sent
// back unchanged, as GitHub does; a 304 is the call GitHub does not count against the quota. Each
// call is recorded with whether it was billed.
sealed class FakeGitHub : HttpMessageHandler
{
    public const string Repo = "acme/app";
    const string Api = $"/repos/{Repo}/";

    static int _instances;

    readonly Lock _gate = new();
    readonly List<(string Call, bool Billed)> _calls = [];
    // RefLock keeps a lock's tag and SnapshotReader a PR's thread by its updatedAt for the process: each
    // fake's tags and PR updates are its own, so what one test read is never another's.
    readonly int _instance = Interlocked.Increment(ref _instances);
    string Tags => $"tag{_instance}x";
    string PrUpdated => new DateTimeOffset(2026, 11, 8, 0, 0, 0, TimeSpan.Zero).AddSeconds(_instance).ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);

    public int Prs { get; init; }
    public int[] Locked { get; init; } = [];
    public string Main { get; set; } = "m1";

    // When set, every REST read answers 403 with no calls left and the quota's reset this far ahead.
    public TimeSpan? RestLimitedFor { get; set; }

    public IReadOnlyList<(string Call, bool Billed)> Calls
    {
        get { lock (_gate) return _calls.ToList(); }
    }

    public int Count => Calls.Count;

    // The REST calls GitHub counts, from the index-th call on, by call site.
    public IReadOnlyList<string> Billed(int from = 0)
    {
        return Calls.Skip(from).Where(c => c.Billed && !c.Call.StartsWith("POST /graphql", StringComparison.Ordinal)).Select(c => c.Call).ToList();
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.PathAndQuery;
        if (request.Method == HttpMethod.Post && path == "/graphql")
        {
            var query = await request.Content!.ReadAsStringAsync(ct);
            Record($"POST /graphql {Name(query)}", true);
            return Json(request, HttpStatusCode.OK, Graph(query));
        }
        if (RestLimitedFor is { } wait)
        {
            Record($"{request.Method} {path}", true);
            var limited = Json(request, HttpStatusCode.Forbidden, """{"message":"API rate limit exceeded"}""");
            limited.Headers.Add("x-ratelimit-remaining", "0");
            limited.Headers.Add("x-ratelimit-reset", (DateTimeOffset.UtcNow + wait).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture));
            return limited;
        }
        var body = Rest(path);
        var tag = $"\"{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body)))[..16]}\"";
        if (request.Headers.IfNoneMatch.Any(t => t.Tag == tag))
        {
            Record($"{request.Method} {path}", false);
            return new HttpResponseMessage(HttpStatusCode.NotModified) { RequestMessage = request, Content = new StringContent("") };
        }
        Record($"{request.Method} {path}", true);
        var response = Json(request, HttpStatusCode.OK, body);
        response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue(tag);
        return response;
    }

    void Record(string call, bool billed)
    {
        lock (_gate) _calls.Add((call, billed));
    }

    static HttpResponseMessage Json(HttpRequestMessage request, HttpStatusCode status, string body)
    {
        return new HttpResponseMessage(status) { RequestMessage = request, Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    static string Name(string query)
    {
        return query.Contains("states:MERGED", StringComparison.Ordinal) ? "Merged"
            : query.Contains("pullRequests(", StringComparison.Ordinal) ? "OpenPrs"
            : query.Contains("defaultBranchRef", StringComparison.Ordinal) ? "Statuses"
            : query.Contains("states:CLOSED", StringComparison.Ordinal) ? "ClosedSince"
            : query.Contains("issues(", StringComparison.Ordinal) ? "Stories"
            : "other";
    }

    // Story n has PR n's head when n ≤ Prs; the locked stories are claimed by another machine.
    IEnumerable<int> Stories => Enumerable.Range(1, Math.Max(Prs, Locked.DefaultIfEmpty(0).Max()) + 1);

    static string Sha(int pr)
    {
        return $"head{pr}";
    }

    string Graph(string query)
    {
        var data = Name(query) switch
        {
            "OpenPrs" => new { repository = new { pullRequests = new
            {
                pageInfo = new { hasNextPage = false, endCursor = (string?)null },
                nodes = Enumerable.Range(1, Prs).Select(n => new
                {
                    number = 100 + n, title = $"app#{n}: story {n}", headRefName = $"story/app-{n}", headRefOid = Sha(n), isDraft = false,
                    mergeable = "MERGEABLE", updatedAt = PrUpdated, headRepository = new { nameWithOwner = Repo }, author = new { login = "bot" },
                }),
            } } } as object,
            "Statuses" => new { repository = new { defaultBranchRef = new { target = new { history = new { nodes = Array.Empty<object>() } } } } },
            "ClosedSince" => new { repository = new { issues = new { pageInfo = new { hasNextPage = false, endCursor = (string?)null }, nodes = Array.Empty<object>() } } },
            "Merged" => new { repository = new { pullRequests = new { pageInfo = new { hasNextPage = false, endCursor = (string?)null }, nodes = Array.Empty<object>() } } },
            _ => new { repository = new { issues = new
            {
                pageInfo = new { hasNextPage = false, endCursor = (string?)null },
                nodes = Stories.Select(n => new
                {
                    number = n, title = $"story {n}", createdAt = "2026-11-01T00:00:00Z", updatedAt = "2026-11-08T00:00:00Z",
                    projectItems = new { nodes = new[] { new { id = $"i{n}", updatedAt = "2026-11-08T00:00:00Z", project = new { number = 1 }, fieldValueByName = new { name = Locked.Contains(n) ? Stages.InProgress : Stages.Ready } } } },
                    milestone = (object?)null,
                    labels = new { nodes = Locked.Contains(n) ? new[] { new { name = Labels.Claimed } } : [] },
                    subIssuesSummary = new { total = 0, percentCompleted = 0 },
                    parent = (object?)null,
                    blockedBy = new { nodes = Array.Empty<object>() },
                }),
            } } },
        };
        return JsonSerializer.Serialize(new { data });
    }

    string Rest(string path)
    {
        if (path.StartsWith(Api, StringComparison.Ordinal)) path = path[Api.Length..];
        var holder = JsonSerializer.Serialize(new LockHolder("other", "job", "Dev", DateTimeOffset.UtcNow.AddMinutes(-5), "zzzz"));
        return path switch
        {
            _ when path.StartsWith("actions/runs?head_sha=", StringComparison.Ordinal) => Runs(path.Split('=', '&')[1]),
            _ when path.StartsWith("actions/runs", StringComparison.Ordinal) => """{"workflow_runs":[]}""",
            _ when path.StartsWith("commits/", StringComparison.Ordinal) => JsonSerializer.Serialize(new { sha = Main }),
            _ when path.StartsWith("releases", StringComparison.Ordinal) => "[]",
            _ when path.StartsWith("git/matching-refs/locks/", StringComparison.Ordinal) => JsonSerializer.Serialize(Locked.Select(n => new { @ref = $"refs/locks/app-{n}", @object = new { sha = $"{Tags}{n}", type = "tag" } })),
            _ when path.StartsWith("git/ref/locks/app-", StringComparison.Ordinal) => JsonSerializer.Serialize(new { @object = new { sha = $"{Tags}{path.Split('-').Last()}", type = "tag" } }),
            _ when path.StartsWith("git/tags/", StringComparison.Ordinal) => JsonSerializer.Serialize(new { message = holder }),
            _ when path.Contains("/events", StringComparison.Ordinal) => """[{"event":"labeled","label":{"name":"claimed"},"created_at":"2026-11-08T00:00:00Z"}]""",
            _ when path.Contains("/reviews", StringComparison.Ordinal) || path.Contains("/comments", StringComparison.Ordinal) => "[]",
            "/user" => """{"login":"bot"}""",
            _ => throw new InvalidOperationException($"the fake GitHub has no answer for {path}"),
        };
    }

    static string Runs(string sha)
    {
        return JsonSerializer.Serialize(new { workflow_runs = new[] { new
        {
            id = 1, workflow_id = 1, head_sha = sha, @event = "pull_request", status = "completed", conclusion = "success",
            run_started_at = "2026-11-08T00:00:00Z", updated_at = "2026-11-08T00:10:00Z", path = ".github/workflows/ci.yml",
        } } });
    }

    // The harness over this GitHub: its real board, repository and locks, a loop in Live with no worker
    // (it plans nothing and starts nothing, so a pass is only its reads), and its own temporary root.
    public sealed class Harness : IDisposable
    {
        readonly string _root = Directory.CreateTempSubdirectory("fake-github").FullName;
        readonly HarnessMetrics _metrics = new();

        public HarnessLoop Loop { get; }

        public Harness(FakeGitHub hub, HarnessOptions? options = null)
        {
            var o = options ?? new HarnessOptions { Machine = "box", Mode = HarnessMode.Live, Repositories = [new RepositoryOptions { Name = Repo }] };
            var gh = new GitHubClient(new HttpClient(hub) { BaseAddress = new Uri("https://api.github.com/") }) { Delay = (_, _) => Task.CompletedTask };
            var board = new GitHubBoard(gh, Repo, new GitHubBoardOptions { Repository = Repo, Project = 1 });
            var repo = new GitHubRepository(gh, o.Repositories[0], o.Names(), "t");
            var repos = new RepositorySet([(repo, o.Repositories[0])]);
            var locks = new RefLock(gh, Repo, "app", "main");
            var leases = new BuildLeases(1);
            var workspace = new Workspace(Path.Combine(_root, "ws"), $"https://example.com/{Repo}.git", null, 1, "Gru", "gru@example.com");
            var jobs = new JobContext(board, repos, new Dictionary<string, Workspace> { [Repo] = workspace }, new NoAgent(), o, [], NullLoggerFactory.Instance, leases);
            Loop = new HarnessLoop(new SnapshotReader(board, repos, locks, o), board, locks, workspace, new JobHistory(Path.Combine(_root, "history")),
                new JobStats(null), jobs, leases, null!, Options.Create(o), null!, _metrics, new WatchState(null), new HarnessState(Path.Combine(_root, "state")),
                NullLogger<HarnessLoop>.Instance);
        }

        // Starts the loop and waits for its first pass.
        public async Task<Pass> Start()
        {
            await Loop.StartAsync(CancellationToken.None);
            return await NextPass(null);
        }

        // The pass after `after`, within a few seconds.
        public async Task<Pass> NextPass(Pass? after, TimeSpan? within = null)
        {
            var until = DateTimeOffset.UtcNow + (within ?? TimeSpan.FromSeconds(10));
            while (DateTimeOffset.UtcNow < until)
            {
                if (Loop.Last is { } last && !ReferenceEquals(last, after)) return last;
                await Task.Delay(20);
            }
            throw new TimeoutException("no pass came");
        }

        public void Dispose()
        {
            Loop.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
            Loop.Dispose();
            _metrics.Dispose();
            Directory.Delete(_root, true);
        }
    }
}
