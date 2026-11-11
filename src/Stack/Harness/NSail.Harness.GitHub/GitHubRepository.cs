// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Collections.Concurrent;
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
        query($o:String!,$n:String!,$c:String){repository(owner:$o,name:$n){pullRequests(first:100,states:MERGED,orderBy:{field:UPDATED_AT,direction:DESC},after:$c){pageInfo{hasNextPage endCursor} nodes{number title headRefName mergedAt updatedAt mergeCommit{oid} headRepository{nameWithOwner} author{login}}}}}
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

        var open = raw.Where(pr => !pr.GetProperty("isDraft").GetBoolean()).ToList();

        // One wide read of the newest runs; a head it did not bring (a PR gone quiet) is read by its sha.
        var window = await gh.Get($"repos/{repo}/actions/runs?event=pull_request&per_page=100", ct);
        var bySha = window.GetProperty("workflow_runs").EnumerateArray()
            .GroupBy(r => r.GetProperty("head_sha").GetString() ?? "")
            .ToDictionary(g => g.Key, g => g.ToList());
        foreach (var sha in open.Select(pr => pr.GetProperty("headRefOid").GetString() ?? "").Distinct().Where(sha => !bySha.ContainsKey(sha)))
        {
            bySha[sha] = await gh.GetAll($"repos/{repo}/actions/runs?head_sha={Uri.EscapeDataString(sha)}&event=pull_request&per_page=100", b => b.GetProperty("workflow_runs"), ct: ct);
        }

        return open.Select(pr =>
        {
            var sha = pr.GetProperty("headRefOid").GetString() ?? "";
            var shaRuns = bySha.GetValueOrDefault(sha);
            var ci = pr.GetProperty("mergeable").GetString() == "CONFLICTING" ? CiState.Conflict : CiOf(shaRuns);
            return new OpenPr(pr.GetProperty("number").GetInt32(), pr.GetProperty("title").GetString() ?? "",
                pr.GetProperty("headRefName").GetString() ?? "", sha, ci, repo, pr.GetProperty("updatedAt").GetString() ?? "",
                ci == CiState.Green ? GreenAtOf(shaRuns) : null,
                NestedString(pr, "headRepository", "nameWithOwner"), NestedString(pr, "author", "login"));
        }).ToList();
    }

    static string NestedString(JsonElement e, string obj, string name)
    {
        return e.TryGetProperty(obj, out var o) && o.ValueKind == JsonValueKind.Object && o.TryGetProperty(name, out var v) ? v.GetString() ?? "" : "";
    }

    public async Task<IReadOnlyList<PrRef>> PrsOf(int story, string state, CancellationToken ct = default)
    {
        if (state != "open")
        {
            return (await Merged(ct)).Where(m => m.Story == story).Select(m => m.Pr).OrderBy(pr => pr.MergedAt).ToList();
        }
        var result = new List<PrRef>();
        foreach (var pr in await gh.GetAll($"repos/{repo}/pulls?state=open&per_page=100", ct: ct))
        {
            var title = pr.GetProperty("title").GetString() ?? "";
            var head = pr.GetProperty("head").GetProperty("ref").GetString() ?? "";
            if (names.StoryOf(title, head) != story || !IsOwnHead(pr)) continue;
            result.Add(new PrRef(pr.GetProperty("number").GetInt32(), title, head, null, repo));
        }
        return result;
    }

    // Every merged PR of the crew that names a story, by its number. The provider lists them newest
    // change first: the first read takes them all, and a later one stops at the first PR last changed
    // before the newest the read before it saw, less an overlap for a merge the list showed late.
    internal TimeSpan MergedFresh { get; set; } = TimeSpan.FromSeconds(10);
    static readonly TimeSpan MergedOverlap = TimeSpan.FromMinutes(10);
    readonly ConcurrentDictionary<int, (PrRef Pr, int Story)> _merged = new();
    readonly SemaphoreSlim _mergedGate = new(1, 1);
    DateTimeOffset? _mergedThrough;
    DateTimeOffset _mergedReadAt;

    async Task<IReadOnlyCollection<(PrRef Pr, int Story)>> Merged(CancellationToken ct)
    {
        await _mergedGate.WaitAsync(ct);
        try
        {
            if (DateTimeOffset.UtcNow - _mergedReadAt < MergedFresh) return _merged.Values.ToList();
            var since = _mergedThrough - MergedOverlap;
            DateTimeOffset? newest = null;
            string? cursor = null;
            do
            {
                var data = await gh.GraphQl(MergedQuery, new { o = Owner, n = repo.Split('/')[1], c = cursor }, ct);
                var page = data.GetProperty("repository").GetProperty("pullRequests");
                var info = page.GetProperty("pageInfo");
                cursor = info.GetProperty("hasNextPage").GetBoolean() ? info.GetProperty("endCursor").GetString() : null;
                foreach (var pr in page.GetProperty("nodes").EnumerateArray())
                {
                    var updated = pr.GetProperty("updatedAt").GetDateTimeOffset();
                    newest ??= updated;
                    if (updated < since)
                    {
                        cursor = null;
                        break;
                    }
                    Keep(pr);
                }
            }
            while (cursor is not null);
            _mergedThrough = newest ?? _mergedThrough;
            _mergedReadAt = DateTimeOffset.UtcNow;
            return _merged.Values.ToList();
        }
        finally
        {
            _mergedGate.Release();
        }
    }

    void Keep(JsonElement pr)
    {
        var number = pr.GetProperty("number").GetInt32();
        var title = pr.GetProperty("title").GetString() ?? "";
        var head = pr.GetProperty("headRefName").GetString() ?? "";
        if (names.StoryOf(title, head) is not { } story || !IsCrews(NestedString(pr, "headRepository", "nameWithOwner"), NestedString(pr, "author", "login")))
        {
            _merged.TryRemove(number, out _);
            return;
        }
        var sha = pr.GetProperty("mergeCommit").ValueKind == JsonValueKind.Object ? pr.GetProperty("mergeCommit").GetProperty("oid").GetString() : null;
        _merged[number] = (new PrRef(number, title, head, pr.GetProperty("mergedAt").GetDateTimeOffset(), repo, sha), story);
    }

    // The planner's own rule (SnapshotReader.IsCrews) over the REST shape.
    bool IsOwnHead(JsonElement pr)
    {
        var head = pr.GetProperty("head");
        var headRepo = head.TryGetProperty("repo", out var hr) && hr.ValueKind == JsonValueKind.Object && hr.TryGetProperty("full_name", out var fn) ? fn.GetString() ?? "" : "";
        return IsCrews(headRepo, NestedString(pr, "user", "login"));
    }

    bool IsCrews(string headRepo, string author)
    {
        return string.Equals(headRepo, repo, StringComparison.OrdinalIgnoreCase)
            && (options.Authors.Count == 0 || options.Authors.Contains(author, StringComparer.OrdinalIgnoreCase));
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
                all.Add(new PrNote(at.GetDateTimeOffset(), r.GetProperty("body").GetString() ?? "", LoginOf(r)));
            }
        }
        foreach (var c in await gh.GetAll($"repos/{repo}/issues/{pr}/comments?per_page=100", ct: ct))
        {
            all.Add(new PrNote(c.GetProperty("created_at").GetDateTimeOffset(), c.GetProperty("body").GetString() ?? "", LoginOf(c)));
        }
        return all.OrderBy(b => b.At).ToList();
    }

    static string LoginOf(JsonElement e)
    {
        return e.TryGetProperty("user", out var u) && u.ValueKind == JsonValueKind.Object && u.TryGetProperty("login", out var l) ? l.GetString() ?? "" : "";
    }

    string? _login;

    public async Task<string> Login(CancellationToken ct = default)
    {
        return _login ??= (await gh.Get("user", ct)).GetProperty("login").GetString() ?? "";
    }

    public async Task<IReadOnlyList<string>> Files(int pr, CancellationToken ct = default)
    {
        // 30 pages of 100: GitHub lists no more than 3,000 files of a PR.
        var files = await gh.GetAll($"repos/{repo}/pulls/{pr}/files?per_page=100", maxPages: 30, ct: ct);
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

    public async Task Merge(int pr, string sha, CancellationToken ct = default)
    {
        // The head is read before the merge: once the merge has gone through, nothing after it may
        // throw, or the caller would report a merged PR as refused.
        var read = await Pr(pr, ct);
        var head = read.HeadRef;
        var ownHead = string.Equals(read.HeadRepo, repo, StringComparison.OrdinalIgnoreCase);
        var merged = await gh.Send(HttpMethod.Put, $"repos/{repo}/pulls/{pr}/merge", new { merge_method = "squash", sha }, ct);
        if (!ownHead) return;
        // The provider's list shows a merge a moment late: this harness's own is known at once.
        if (names.StoryOf(read.Title, head) is { } story && merged.ValueKind == JsonValueKind.Object && merged.TryGetProperty("sha", out var commit) && commit.ValueKind == JsonValueKind.String)
        {
            _merged[pr] = (new PrRef(pr, read.Title, head, DateTimeOffset.UtcNow, repo, commit.GetString()), story);
        }
        try
        {
            await gh.Send(HttpMethod.Delete, $"repos/{repo}/git/refs/heads/{head}", ct: ct);
        }
        catch (Exception e) when (e is ProviderException or HttpRequestException or TaskCanceledException)
        {
        }
    }

    public async Task<Merge?> MergeOf(int story, CancellationToken ct = default)
    {
        var newest = (await Merged(ct)).Where(m => m.Story == story).Select(m => m.Pr).MaxBy(pr => pr.MergedAt);
        return newest is { MergeSha: { } sha, MergedAt: { } at } ? new Merge(sha, at) : null;
    }

    public async Task<CiState> Ci(string branch, string sha, CancellationToken ct = default)
    {
        var runs = await gh.GetAll($"repos/{repo}/actions/runs?head_sha={Uri.EscapeDataString(sha)}&per_page=100", b => b.GetProperty("workflow_runs"), ct: ct);
        return CiOf(runs.Where(r => r.GetProperty("head_sha").GetString() == sha
            && r.GetProperty("event").GetString() is "pull_request" or "push").ToList());
    }

    public async Task<CiFailure?> Failure(string sha, CancellationToken ct = default)
    {
        var runs = await gh.GetAll($"repos/{repo}/actions/runs?head_sha={Uri.EscapeDataString(sha)}&per_page=100", b => b.GetProperty("workflow_runs"), ct: ct);
        return await FailureOf(runs.Where(r => r.GetProperty("head_sha").GetString() == sha
            && r.GetProperty("event").GetString() is "pull_request" or "push").ToList(), ct);
    }

    public async Task<CiFailure?> MainFailure(CancellationToken ct = default)
    {
        var runs = await gh.Get($"repos/{repo}/actions/runs?branch={Uri.EscapeDataString(options.DefaultBranch)}&event=push&status=completed&per_page=50", ct);
        return await FailureOf(runs.GetProperty("workflow_runs").EnumerateArray().ToList(), ct);
    }

    // A run's failed jobs never change once it completed: each attempt is read once, and kept reduced,
    // the newest few hundred runs only.
    const int KeptRuns = 256;
    readonly ConcurrentDictionary<(long Run, int Attempt), List<CiFailure>> _failedJobs = new();

    async Task<CiFailure?> FailureOf(List<JsonElement> runs, CancellationToken ct)
    {
        var failed = Latest(runs).Where(r => r.GetProperty("status").GetString() == "completed" && IsRed(Conclusion(r))).ToList();
        if (failed.Count == 0) return null;
        var jobs = new List<CiFailure>();
        foreach (var run in failed)
        {
            var key = (run.GetProperty("id").GetInt64(), run.TryGetProperty("run_attempt", out var a) ? a.GetInt32() : 1);
            if (!_failedJobs.TryGetValue(key, out var hit))
            {
                hit = [];
                var page = await gh.GetAll($"repos/{repo}/actions/runs/{key.Item1}/jobs?per_page=100", b => b.GetProperty("jobs"), ct: ct);
                foreach (var (id, job, step) in FailedJobs(page))
                {
                    hit.Add(CiFailure.Of(job, step, await LogOf(id, ct)));
                }
                // A run red with no job failed (a workflow that never started) is named by itself.
                if (hit.Count == 0) hit.Add(CiFailure.Of(run.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "", Conclusion(run), ""));
                if (_failedJobs.Count >= KeptRuns) _failedJobs.Clear();
                _failedJobs[key] = hit;
            }
            jobs.AddRange(hit);
        }
        return CiFailure.Of(jobs);
    }

    // A run's failed jobs, each with its first failed step.
    internal static IEnumerable<(long Id, string Job, string Step)> FailedJobs(IEnumerable<JsonElement> jobs)
    {
        foreach (var job in jobs.Where(j => IsRed(Conclusion(j))))
        {
            var steps = job.TryGetProperty("steps", out var s) && s.ValueKind == JsonValueKind.Array ? s.EnumerateArray().ToList() : [];
            var step = steps.FirstOrDefault(st => IsRed(Conclusion(st)));
            yield return (job.GetProperty("id").GetInt64(), job.GetProperty("name").GetString() ?? "",
                step.ValueKind == JsonValueKind.Object ? step.GetProperty("name").GetString() ?? "" : Conclusion(job));
        }
    }

    // A job's log expires with the run's retention: what is gone is said, not thrown.
    async Task<string> LogOf(long job, CancellationToken ct)
    {
        try
        {
            return await gh.GetText($"repos/{repo}/actions/jobs/{job}/logs", ct);
        }
        catch (GitHubException e) when (e.HttpStatus is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            return "(log gone)";
        }
    }

    static string Conclusion(JsonElement e)
    {
        return e.TryGetProperty("conclusion", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() ?? "" : "";
    }

    static bool IsRed(string conclusion)
    {
        return conclusion is "failure" or "timed_out" or "cancelled" or "startup_failure" or "stale";
    }

    // The newest run per workflow at a head.
    static IEnumerable<JsonElement> Latest(List<JsonElement> runs)
    {
        return runs
            .GroupBy(r => r.GetProperty("workflow_id").GetInt64())
            .Select(g => g.OrderBy(r => r.TryGetProperty("run_started_at", out var s) ? s.GetString() : "").Last());
    }

    // When a green head's last workflow finished: a run's updated_at is its completion once completed.
    internal static DateTimeOffset? GreenAtOf(List<JsonElement>? runs)
    {
        if (runs is null || runs.Count == 0) return null;
        return Latest(runs).Select(r => r.TryGetProperty("updated_at", out var u) && u.ValueKind == JsonValueKind.String ? u.GetDateTimeOffset() : (DateTimeOffset?)null).Max();
    }

    internal static CiState CiOf(List<JsonElement>? runs)
    {
        if (runs is null || runs.Count == 0) return CiState.Pending;
        var latest = Latest(runs);
        var pending = false;
        var red = false;
        foreach (var run in latest)
        {
            var status = run.GetProperty("status").GetString();
            var conclusion = Conclusion(run);
            if (status != "completed") pending = true;
            else if (IsRed(conclusion)) red = true;
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
            catch (GitHubException e) when (e.HttpStatus == HttpStatusCode.NotFound)
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
                .Where(c => c.GetProperty("state").GetString() is "SUCCESS" or "FAILURE" or "ERROR" or "PENDING")
                .Select(c => new StatusMark(sha, c.GetProperty("context").GetString()!, c.GetProperty("state").GetString() == "SUCCESS", c.GetProperty("createdAt").GetDateTimeOffset(), c.GetProperty("state").GetString() == "PENDING"))
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
        // The newest 100: every reader takes the head of the list.
        var releases = (await gh.Get($"repos/{repo}/releases?per_page=100", ct)).EnumerateArray();
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

    public async Task<IReadOnlyList<Merge>> Changes(string path, DateTimeOffset since, CancellationToken ct = default)
    {
        var at = since.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
        var commits = await gh.GetAll($"repos/{repo}/commits?sha={Uri.EscapeDataString(options.DefaultBranch)}&path={Uri.EscapeDataString(path)}&since={at}&per_page=100", ct: ct);
        return commits
            .Select(c => new Merge(c.GetProperty("sha").GetString()!, c.GetProperty("commit").GetProperty("committer").GetProperty("date").GetDateTimeOffset()))
            .OrderBy(c => c.At).ToList();
    }

    public async Task<string?> FileAt(string path, string reference, CancellationToken ct = default)
    {
        try
        {
            var file = await gh.Get($"repos/{repo}/contents/{string.Join('/', path.Split('/').Select(Uri.EscapeDataString))}?ref={Uri.EscapeDataString(reference)}", ct);
            if (file.ValueKind != JsonValueKind.Object || !file.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.String) return null;
            return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(content.GetString()!));
        }
        catch (GitHubException e) when (e.HttpStatus == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<string?> Variable(string name, CancellationToken ct = default)
    {
        var v = await gh.Get($"repos/{repo}/actions/variables/{name}", ct);
        return v.GetProperty("value").GetString();
    }
}
