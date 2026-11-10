// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net;
using System.Text;
using System.Text.Json;
using NSail.Harness.GitHub;
using NSail.Harness.Repositories;

namespace NSail.Harness.Tests;

// A story's merged PRs come off the repository's own list of merged PRs, read whole once and then only
// as far as what changed since: the search index, and its budget every job on the token shares, is
// never asked.
public class GitHubMergedTests
{
    static readonly DateTimeOffset Now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    static string Node(int number, int story, DateTimeOffset at, string headRepo = "o/r", string author = "crew")
    {
        return JsonSerializer.Serialize(new
        {
            number, title = $"app#{story}: x", headRefName = $"story/app-{story}", mergedAt = at, updatedAt = at,
            mergeCommit = new { oid = $"m{number}" }, headRepository = new { nameWithOwner = headRepo }, author = new { login = author },
        });
    }

    // The merged list in pages, newest change first; every other call is a PR read, its merge, or a branch delete.
    sealed class Hub(params string[][] pages) : HttpMessageHandler
    {
        public List<string> Sent { get; } = [];

        public int Listed => Sent.Count(s => s == "POST /graphql");

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            Sent.Add($"{request.Method} {path}");
            string body;
            if (path == "/graphql")
            {
                using var sent = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
                var cursor = sent.RootElement.GetProperty("variables").GetProperty("c");
                var page = cursor.ValueKind == JsonValueKind.String ? int.Parse(cursor.GetString()!, System.Globalization.CultureInfo.InvariantCulture) : 0;
                var more = page + 1 < pages.Length;
                var nodes = pages.Length == 0 ? "" : string.Join(',', pages[page]);
                body = JsonSerializer.Serialize(new { data = new { repository = new { pullRequests = new { pageInfo = new { hasNextPage = more, endCursor = $"{page + 1}" }, nodes = JsonSerializer.Deserialize<JsonElement>($"[{nodes}]") } } } });
            }
            else if (request.Method == HttpMethod.Put)
            {
                body = """{"sha":"own","merged":true}""";
            }
            else
            {
                body = """{"title":"app#9: x","state":"open","head":{"ref":"story/app-9","sha":"abc","repo":{"full_name":"o/r"}}}""";
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request, Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    static GitHubRepository Repo(Hub hub, TimeSpan? fresh = null)
    {
        var gh = new GitHubClient(new HttpClient(hub) { BaseAddress = new Uri("https://api.github.com/") });
        return new GitHubRepository(gh, new RepositoryOptions { Name = "o/r", Authors = ["crew"] }, new StoryNames("app", ["story"]), "t")
        {
            MergedFresh = fresh ?? TimeSpan.FromMinutes(1),
        };
    }

    [Fact]
    public async Task AStorysMergedPrsAreReadOffEveryPageOfTheListAndNeverSearched()
    {
        var hub = new Hub([Node(8, 3, Now), Node(7, 4, Now.AddHours(-1))], [Node(6, 3, Now.AddDays(-1))]);
        var repo = Repo(hub);

        var prs = await repo.PrsOf(3, "merged");

        Assert.Equal([6, 8], prs.Select(p => p.Number));
        Assert.Equal("m8", prs[1].MergeSha);
        Assert.Equal(2, hub.Listed);
        Assert.DoesNotContain(hub.Sent, s => s.Contains("/search/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheStorysMergeIsItsNewest()
    {
        var repo = Repo(new Hub([Node(8, 3, Now), Node(6, 3, Now.AddDays(-1))]));

        Assert.Equal(new Merge("m8", Now), await repo.MergeOf(3));
        Assert.Null(await repo.MergeOf(4));
    }

    [Fact]
    public async Task EveryStoryAskedWithinTheSameMomentSharesOneRead()
    {
        var hub = new Hub([Node(8, 3, Now), Node(7, 4, Now)]);
        var repo = Repo(hub);

        await repo.PrsOf(3, "merged");
        await repo.MergeOf(4);
        await repo.MergeOf(5);

        Assert.Equal(1, hub.Listed);
    }

    [Fact]
    public async Task ALaterReadStopsAtWhatTheReadBeforeItSaw()
    {
        var hub = new Hub([Node(8, 3, Now), Node(7, 4, Now.AddHours(-1))], [Node(6, 3, Now.AddDays(-1))]);
        var repo = Repo(hub, TimeSpan.Zero);

        await repo.PrsOf(3, "merged");
        var prs = await repo.PrsOf(3, "merged");

        Assert.Equal([6, 8], prs.Select(p => p.Number));
        Assert.Equal(3, hub.Listed);
    }

    [Fact]
    public async Task APrOfAForkOrOfAnUnlistedAuthorIsNoStorysMerge()
    {
        var repo = Repo(new Hub([Node(8, 3, Now, headRepo: "fork/r"), Node(7, 3, Now, author: "stranger")]));

        Assert.Empty(await repo.PrsOf(3, "merged"));
        Assert.Null(await repo.MergeOf(3));
    }

    [Fact]
    public async Task AMergeThisHarnessMadeIsKnownBeforeTheListShowsIt()
    {
        var repo = Repo(new Hub(), TimeSpan.Zero);

        await repo.Merge(1, "abc");

        Assert.Equal("own", (await repo.MergeOf(9))?.Sha);
        Assert.Equal(1, Assert.Single(await repo.PrsOf(9, "merged")).Number);
    }
}
