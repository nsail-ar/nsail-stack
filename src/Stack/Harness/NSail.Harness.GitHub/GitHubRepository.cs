// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net;
using System.Text.Json;
using NSail.Harness.Repositories;

namespace NSail.Harness.GitHub;

// The code on GitHub: a story's PRs are titled `{key}#{n}:` or headed `{prefix}/{key}-{n}*` here;
// PR comments share the issues API.
public sealed class GitHubRepository(GitHubClient gh, RepositoryOptions options, StoryNames names, string token) : IRepository
{
    const string PrsQuery = """
        query($o:String!,$n:String!,$c:String){repository(owner:$o,name:$n){pullRequests(first:100,states:OPEN,after:$c){pageInfo{hasNextPage endCursor} nodes{number title headRefName headRefOid isDraft mergeable updatedAt headRepository{nameWithOwner} author{login}}}}}
        """;

    const string StatusesQuery = """
        query($o:String!,$n:String!,$d:Int!){repository(owner:$o,name:$n){defaultBranchRef{target{... on Commit{history(first:$d){nodes{oid status{contexts{context state createdAt}}}}}}}}}
        """;

    const string MergedQuery = """
        query($q:String!){search(query:$q,type:ISSUE,first:30){nodes{... on PullRequest{title headRefName mergedAt mergeCommit{oid}}}}}
        """;

    readonly string repo = options.Name;
    readonly string prefix = options.BranchPrefix;

    public string Name => repo;
    public string DefaultBranch => options.DefaultBranch;

    public string CloneUrl => $"https://github.com/{repo}.git";

    // gh, with the token in the lane's GH_TOKEN.
    public string? CredentialHelper => "!gh auth git-credential";

    string Owner => repo.Split('/')[0];

    public async Task<IReadOnlyList<OpenPr>> OpenPrs(CancellationToken ct = default)
    {
        var raw = new List<JsonElement>();
        string? cursor = null;
        do
        {
            var data = await gh.GraphQl(PrsQuery, new { o = Owner, n = repo.Split('/')[1], c = cursor }, ct);
            var page = data.GetProperty("repository").GetProperty("pullRequests");
            raw.AddRange(page.GetProperty("nodes").EnumerateArray());
            var info = page.GetProperty("pageInfo");
            cursor = info.GetProperty("hasNextPage").GetBoolean() ? info.GetProperty("endCursor").GetString() : null;
        }
        while (cursor is not null);

        // CI on every PR head in one read: the newest run per workflow for each sha.
        var runs = await gh.GetAll($"repos/{repo}/actions/runs?event=pull_request&per_page=100", b => b.GetProperty("workflow_runs"), maxPages: 1, ct: ct);
        var bySha = runs.GroupBy(r => r.GetProperty("head_sha").GetString() ?? "").ToDictionary(g => g.Key, g => g.ToList());

        return raw.Where(pr => !pr.GetProperty("isDraft").GetBoolean()).Select(pr =>
        {
            var sha = pr.GetProperty("headRefOid").GetString() ?? "";
            var ci = pr.GetProperty("mergeable").GetString() == "CONFLICTING" ? CiState.Conflict : CiOf(bySha.GetValueOrDefault(sha));
            return new OpenPr(pr.GetProperty("number").GetInt32(), pr.GetProperty("title").GetString() ?? "",
                pr.GetProperty("headRefName").GetString() ?? "", sha, ci, repo, pr.GetProperty("updatedAt").GetString() ?? "",
                NestedString(pr, "headRepository", "nameWithOwner"), NestedString(pr, "author", "login"));
        }).ToList();
    }

    static string NestedString(JsonElement e, string obj, string name)
    {
        return e.TryGetProperty(obj, out var o) && o.ValueKind == JsonValueKind.Object && o.TryGetProperty(name, out var v) ? v.GetString() ?? "" : "";
    }

    public async Task<IReadOnlyList<PrRef>> PrsOf(int story, string state, CancellationToken ct = default)
    {
        var result = new List<PrRef>();
        var q = Uri.EscapeDataString($"repo:{repo} is:pr is:{(state == "open" ? "open" : "merged")} {names.Key}-{story} OR {names.Ref(story)}");
        var found = await gh.Get($"search/issues?q={q}&per_page=50", ct);
        foreach (var item in found.GetProperty("items").EnumerateArray())
        {
            var n = item.GetProperty("number").GetInt32();
            var pr = await gh.Get($"repos/{repo}/pulls/{n}", ct);
            var title = pr.GetProperty("title").GetString() ?? "";
            var head = pr.GetProperty("head").GetProperty("ref").GetString() ?? "";
            if (names.StoryOf(title, head) != story) continue;
            if (!IsOwnHead(pr)) continue;
            if (state == "open" && pr.GetProperty("state").GetString() != "open") continue;
            var merged = MergedAt(pr);
            if (state == "merged" && merged is null) continue;
            result.Add(new PrRef(n, title, head, merged, repo, MergeSha(pr)));
        }
        // The search index lags a push by minutes: the plain head is read directly too.
        var direct = await gh.GetAll($"repos/{repo}/pulls?state={(state == "open" ? "open" : "closed")}&head={Owner}:{names.Branch(prefix, story)}&per_page=20", maxPages: 1, ct: ct);
        foreach (var pr in direct)
        {
            var n = pr.GetProperty("number").GetInt32();
            if (result.Any(p => p.Number == n) || !IsOwnHead(pr)) continue;
            var merged = MergedAt(pr);
            if (state == "merged" && merged is null) continue;
            result.Add(new PrRef(n, pr.GetProperty("title").GetString() ?? "", pr.GetProperty("head").GetProperty("ref").GetString() ?? "", merged, repo, MergeSha(pr)));
        }
        return result;
    }

    bool IsOwnHead(JsonElement pr)
    {
        var head = pr.GetProperty("head");
        return head.TryGetProperty("repo", out var hr) && hr.ValueKind == JsonValueKind.Object
            && string.Equals(hr.GetProperty("full_name").GetString(), repo, StringComparison.OrdinalIgnoreCase);
    }

    static DateTimeOffset? MergedAt(JsonElement pr)
    {
        return pr.TryGetProperty("merged_at", out var m) && m.ValueKind == JsonValueKind.String ? m.GetDateTimeOffset() : null;
    }

    static string? MergeSha(JsonElement pr)
    {
        return MergedAt(pr) is not null && pr.TryGetProperty("merge_commit_sha", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;
    }

    public async Task<PullRequest> Pr(int number, CancellationToken ct = default)
    {
        var pr = await gh.Get($"repos/{repo}/pulls/{number}", ct);
        var head = pr.GetProperty("head");
        return new PullRequest(number, pr.GetProperty("title").GetString() ?? "", pr.GetProperty("state").GetString() == "open",
            head.GetProperty("ref").GetString() ?? "", head.GetProperty("sha").GetString() ?? "",
            pr.TryGetProperty("mergeable_state", out var ms) && ms.GetString() == "dirty",
            pr.TryGetProperty("body", out var b) && b.ValueKind == JsonValueKind.String ? b.GetString()! : "",
            head.TryGetProperty("repo", out var hr) && hr.ValueKind == JsonValueKind.Object ? hr.GetProperty("full_name").GetString() ?? "" : "");
    }

    public async Task<IReadOnlyList<PrNote>> Thread(int pr, CancellationToken ct = default)
    {
        var all = new List<PrNote>();
        foreach (var r in await gh.GetAll($"repos/{repo}/pulls/{pr}/reviews?per_page=100", ct: ct))
        {
            if (r.TryGetProperty("submitted_at", out var at) && at.ValueKind == JsonValueKind.String)
            {
                all.Add(new PrNote(at.GetDateTimeOffset(), r.GetProperty("body").GetString() ?? ""));
            }
        }
        foreach (var c in await gh.GetAll($"repos/{repo}/issues/{pr}/comments?per_page=100", ct: ct))
        {
            all.Add(new PrNote(c.GetProperty("created_at").GetDateTimeOffset(), c.GetProperty("body").GetString() ?? ""));
        }
        return all.OrderBy(b => b.At).ToList();
    }

    public async Task<IReadOnlyList<string>> Files(int pr, CancellationToken ct = default)
    {
        var files = await gh.GetAll($"repos/{repo}/pulls/{pr}/files?per_page=100", maxPages: 3, ct: ct);
        return files.Select(f => $"{f.GetProperty("filename").GetString()} (+{f.GetProperty("additions").GetInt32()} -{f.GetProperty("deletions").GetInt32()})").ToList();
    }

    public async Task Comment(int pr, string body)
    {
        await gh.Send(HttpMethod.Post, $"repos/{repo}/issues/{pr}/comments", new { body });
    }

    public async Task<int> CreatePr(string branch, string title, string body, CancellationToken ct = default)
    {
        var created = await gh.Send(HttpMethod.Post, $"repos/{repo}/pulls", new { head = branch, @base = options.DefaultBranch, title, body }, ct);
        return created.GetProperty("number").GetInt32();
    }

    public async Task ClosePr(int pr, string why, CancellationToken ct = default)
    {
        await Comment(pr, why);
        await gh.Send(HttpMethod.Patch, $"repos/{repo}/pulls/{pr}", new { state = "closed" }, ct);
    }

    // gh is the credential with the harness's own token; the empty helper first resets the list, so no
    // machine-wide helper (a window waiting for a click) is asked. The lease names its expected head
    // outright: the remote-tracking refs live in a clone other slots fetch into.
    public async Task Push(string workDir, string branch, string? startedAt, CancellationToken ct = default)
    {
        var env = new Dictionary<string, string> { ["GH_TOKEN"] = token, ["GIT_TERMINAL_PROMPT"] = "0" };
        var result = await Lanes.Shell.Run("git", ["-c", "credential.helper=", "-c", "credential.helper=!gh auth git-credential", "push",
            $"--force-with-lease=refs/heads/{branch}:{startedAt}", "origin", $"HEAD:refs/heads/{branch}"],
            workDir, env, TimeSpan.FromMinutes(10), ct: ct);
        if (result.ExitCode != 0) throw new InvalidOperationException($"push to {branch} refused: {result.Output.Trim()}");
    }

    public async Task Merge(int pr, CancellationToken ct = default)
    {
        // The head is read before the merge: once the merge has gone through, nothing after it may
        // throw, or the caller would report a merged PR as refused.
        var read = await Pr(pr, ct);
        var head = read.HeadRef;
        var ownHead = string.Equals(read.HeadRepo, repo, StringComparison.OrdinalIgnoreCase);
        await gh.Send(HttpMethod.Put, $"repos/{repo}/pulls/{pr}/merge", new { merge_method = "squash" }, ct);
        if (!ownHead) return;
        try
        {
            await gh.Send(HttpMethod.Delete, $"repos/{repo}/git/refs/heads/{head}", ct: ct);
        }
        catch (Exception e) when (e is GitHubException or HttpRequestException or TaskCanceledException)
        {
        }
    }

    public async Task<Merge?> MergeOf(int story, CancellationToken ct = default)
    {
        var data = await gh.GraphQl(MergedQuery, new { q = $"repo:{repo} is:pr is:merged {names.Ref(story)}" }, ct);
        var merge = data.GetProperty("search").GetProperty("nodes").EnumerateArray()
            .Where(n => n.TryGetProperty("title", out _))
            .Where(n => names.StoryOf(n.GetProperty("title").GetString() ?? "", n.GetProperty("headRefName").GetString() ?? "") == story)
            .OrderBy(n => n.GetProperty("mergedAt").GetString())
            .LastOrDefault();
        if (merge.ValueKind != JsonValueKind.Object || merge.GetProperty("mergeCommit").ValueKind != JsonValueKind.Object) return null;
        return new Merge(merge.GetProperty("mergeCommit").GetProperty("oid").GetString()!, merge.GetProperty("mergedAt").GetDateTimeOffset());
    }

    public async Task<CiState> Ci(string branch, string sha, CancellationToken ct = default)
    {
        var runs = await gh.GetAll($"repos/{repo}/actions/runs?branch={Uri.EscapeDataString(branch)}&per_page=20", b => b.GetProperty("workflow_runs"), maxPages: 1, ct: ct);
        return CiOf(runs.Where(r => r.GetProperty("head_sha").GetString() == sha
            && r.GetProperty("event").GetString() is "pull_request" or "push").ToList());
    }

    internal static CiState CiOf(List<JsonElement>? runs)
    {
        if (runs is null || runs.Count == 0) return CiState.Pending;
        var latest = runs
            .GroupBy(r => r.GetProperty("workflow_id").GetInt64())
            .Select(g => g.OrderBy(r => r.TryGetProperty("run_started_at", out var s) ? s.GetString() : "").Last());
        var pending = false;
        var red = false;
        foreach (var run in latest)
        {
            var status = run.GetProperty("status").GetString();
            var conclusion = run.TryGetProperty("conclusion", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : "";
            if (status != "completed") pending = true;
            else if (conclusion is "failure" or "timed_out" or "cancelled" or "startup_failure" or "stale") red = true;
            else if (conclusion is not ("success" or "skipped" or "neutral")) pending = true;
        }
        return pending ? CiState.Pending : red ? CiState.Red : CiState.Green;
    }

    public async Task<string> Head(string branch, CancellationToken ct = default)
    {
        return (await gh.Get($"repos/{repo}/commits/{branch}", ct)).GetProperty("sha").GetString()!;
    }

    public async Task<IReadOnlyList<DateTimeOffset>> PushTimes(int story, CancellationToken ct = default)
    {
        var times = new List<DateTimeOffset>();
        List<JsonElement> refs;
        try
        {
            refs = await gh.GetAll($"repos/{repo}/git/matching-refs/heads/{names.Branch(prefix, story)}", ct: ct);
        }
        catch (GitHubException e) when (e.HttpStatus == HttpStatusCode.NotFound)
        {
            return times;
        }
        foreach (var r in refs)
        {
            var branch = (r.GetProperty("ref").GetString() ?? "")["refs/heads/".Length..];
            if (names.BranchStory(branch) != story) continue;
            try
            {
                var cmp = await gh.Get($"repos/{repo}/compare/{options.DefaultBranch}...{Uri.EscapeDataString(branch)}", ct);
                times.AddRange(cmp.GetProperty("commits").EnumerateArray()
                    .Select(c => c.GetProperty("commit").GetProperty("committer").GetProperty("date").GetDateTimeOffset()));
            }
            catch (GitHubException)
            {
            }
        }
        times.Sort();
        return times;
    }

    public async Task SetStatus(string sha, string context, string state, string description, CancellationToken ct = default)
    {
        await gh.Send(HttpMethod.Post, $"repos/{repo}/statuses/{sha}",
            new { state, context, description = description.Length > 140 ? description[..140] : description }, ct);
    }

    public async Task<IReadOnlyList<StatusMark>> Statuses(int depth = 100, CancellationToken ct = default)
    {
        var data = await gh.GraphQl(StatusesQuery, new { o = Owner, n = repo.Split('/')[1], d = depth }, ct);
        var marks = new List<StatusMark>();
        foreach (var commit in data.GetProperty("repository").GetProperty("defaultBranchRef").GetProperty("target").GetProperty("history").GetProperty("nodes").EnumerateArray())
        {
            if (commit.GetProperty("status").ValueKind != JsonValueKind.Object) continue;
            var sha = commit.GetProperty("oid").GetString()!;
            marks.AddRange(commit.GetProperty("status").GetProperty("contexts").EnumerateArray()
                .Where(c => c.GetProperty("state").GetString() is "SUCCESS" or "FAILURE" or "ERROR")
                .Select(c => new StatusMark(sha, c.GetProperty("context").GetString()!, c.GetProperty("state").GetString() == "SUCCESS", c.GetProperty("createdAt").GetDateTimeOffset()))
                .OrderByDescending(m => m.At));
        }
        return marks;
    }

    public async Task<IReadOnlySet<string>> RunningWorkflows(CancellationToken ct = default)
    {
        var running = new HashSet<string>();
        foreach (var status in new[] { "queued", "in_progress", "waiting", "pending" })
        {
            var runs = await gh.GetAll($"repos/{repo}/actions/runs?status={status}&per_page=100", b => b.GetProperty("workflow_runs"), ct: ct);
            foreach (var run in runs) running.Add((run.GetProperty("path").GetString() ?? "").Split('/').Last());
        }
        return running;
    }

    public async Task<int> RunsSince(string workflow, DateTimeOffset at, CancellationToken ct = default)
    {
        var runs = await gh.Get($"repos/{repo}/actions/workflows/{workflow}/runs?per_page=5", ct);
        return runs.GetProperty("workflow_runs").EnumerateArray()
            .Count(r => r.GetProperty("created_at").GetDateTimeOffset() > at && r.GetProperty("conclusion").GetString() != "cancelled");
    }

    public async Task Dispatch(string workflow, object inputs, CancellationToken ct = default)
    {
        await gh.Send(HttpMethod.Post, $"repos/{repo}/actions/workflows/{workflow}/dispatches", new { @ref = options.DefaultBranch, inputs }, ct);
    }

    public async Task<IReadOnlyList<Release>> Releases(CancellationToken ct = default)
    {
        var releases = await gh.GetAll($"repos/{repo}/releases?per_page=50", maxPages: 1, ct: ct);
        return releases
            .Where(r => !r.GetProperty("draft").GetBoolean() && r.TryGetProperty("published_at", out var p) && p.ValueKind == JsonValueKind.String)
            .Select(r => new Release(r.GetProperty("tag_name").GetString() ?? "", r.GetProperty("published_at").GetDateTimeOffset()))
            .OrderByDescending(r => r.PublishedAt).ToList();
    }

    public async Task<bool> Holds(string reference, string sha, CancellationToken ct = default)
    {
        try
        {
            var compare = await gh.Get($"repos/{repo}/compare/{sha}...{reference}", ct);
            return compare.GetProperty("status").GetString() is "ahead" or "identical";
        }
        // A reference that does not exist (a pin's tag not cut yet) holds nothing.
        catch (GitHubException e) when (e.HttpStatus == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<string?> Variable(string name, CancellationToken ct = default)
    {
        var v = await gh.Get($"repos/{repo}/actions/variables/{name}", ct);
        return v.GetProperty("value").GetString();
    }
}
