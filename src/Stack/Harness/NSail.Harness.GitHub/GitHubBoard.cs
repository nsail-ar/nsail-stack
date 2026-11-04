// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net;
using System.Text.Json;
using NSail.Harness.Board;

namespace NSail.Harness.GitHub;

// The board as GitHub Issues: a story is an issue, its stage its column on the project (Status),
// its holds and locks labels, its epic the sub-issue parent, its blockers the `blocked by` links.
public sealed class GitHubBoard(GitHubClient gh, string repo, GitHubBoardOptions options) : IBoard
{

    const string IssuesQuery = """
        query($o:String!,$n:String!,$c:String,$sf:String!){repository(owner:$o,name:$n){issues(first:100,states:OPEN,after:$c){pageInfo{hasNextPage endCursor} nodes{number title projectItems(first:10){nodes{id project{number} fieldValueByName(name:$sf){... on ProjectV2ItemFieldSingleSelectValue{name}}}} milestone{number} labels(first:30){nodes{name}} subIssuesSummary{total percentCompleted} parent{number subIssuesSummary{percentCompleted}} blockedBy(first:30){nodes{number state}}}}}}
        """;

    const string ClosedQuery = """
        query($o:String!,$n:String!,$s:DateTime!,$sf:String!){repository(owner:$o,name:$n){issues(first:100,states:CLOSED,filterBy:{since:$s},orderBy:{field:UPDATED_AT,direction:DESC}){nodes{number closedAt projectItems(first:10){nodes{id project{number} fieldValueByName(name:$sf){... on ProjectV2ItemFieldSingleSelectValue{name}}}}}}}}
        """;

    public string Name => repo;

    public async Task<IReadOnlyList<(int Number, string Stage)>> ClosedSince(DateTimeOffset since, CancellationToken ct = default)
    {
        var data = await gh.GraphQl(ClosedQuery, new { o = Owner, n = RepoName, s = since.UtcDateTime.ToString("O"), sf = options.StatusField }, ct);
        return data.GetProperty("repository").GetProperty("issues").GetProperty("nodes").EnumerateArray()
            .Where(n => n.GetProperty("closedAt").GetDateTimeOffset() >= since)
            .Select(n => (n.GetProperty("number").GetInt32(), StageIn(n, options.Project)))
            .ToList();
    }

    // Where a story stands: its column on the board's project, a dash when it has none there.
    internal static string StageIn(JsonElement n, int project)
    {
        return StatusIn(n, project) ?? "-";
    }

    internal static string? StatusIn(JsonElement n, int project)
    {
        if (n.TryGetProperty("projectItems", out var items))
        {
            foreach (var item in items.GetProperty("nodes").EnumerateArray())
            {
                if (item.GetProperty("project").GetProperty("number").GetInt32() != project) continue;
                if (item.GetProperty("fieldValueByName") is { ValueKind: JsonValueKind.Object } value
                    && value.TryGetProperty("name", out var name) && name.GetString() is { Length: > 0 } status)
                {
                    return status;
                }
            }
        }
        return null;
    }

    string Owner => repo.Split('/')[0];

    string RepoName => repo.Split('/')[1];

    public async Task<IReadOnlyList<Issue>> Stories(CancellationToken ct = default)
    {
        var issues = new List<Issue>();
        string? cursor = null;
        do
        {
            var data = await gh.GraphQl(IssuesQuery, new { o = Owner, n = RepoName, c = cursor, sf = options.StatusField }, ct);
            var page = data.GetProperty("repository").GetProperty("issues");
            foreach (var n in page.GetProperty("nodes").EnumerateArray())
            {
                issues.Add(ParseIssue(n, options.Project));
            }
            var info = page.GetProperty("pageInfo");
            cursor = info.GetProperty("hasNextPage").GetBoolean() ? info.GetProperty("endCursor").GetString() : null;
        }
        while (cursor is not null);
        if (issues.Count == 0) throw new InvalidOperationException("the issue walk came back empty");
        return issues.OrderBy(i => i.Number).ToList();
    }

    internal static Issue ParseIssue(JsonElement n, int project)
    {
        var labels = n.GetProperty("labels").GetProperty("nodes").EnumerateArray()
            .Select(l => l.GetProperty("name").GetString()!).ToHashSet();
        var stage = StageIn(n, project);
        var subs = n.GetProperty("subIssuesSummary");
        var allBlockers = n.GetProperty("blockedBy").GetProperty("nodes").EnumerateArray().ToList();
        var blockers = allBlockers
            .Where(b => b.GetProperty("state").GetString() == "OPEN")
            .Select(b => b.GetProperty("number").GetInt32()).ToList();
        var parent = n.GetProperty("parent");
        var hasParent = parent.ValueKind == JsonValueKind.Object;
        return new Issue(
            n.GetProperty("number").GetInt32(),
            n.GetProperty("title").GetString() ?? "",
            labels,
            stage,
            subs.GetProperty("total").GetInt32(),
            blockers,
            hasParent ? Percent(parent.GetProperty("subIssuesSummary")) : -1,
            n.GetProperty("milestone").ValueKind == JsonValueKind.Object,
            hasParent ? parent.GetProperty("number").GetInt32() : 0,
            Percent(subs),
            allBlockers.Count);
    }

    static double Percent(JsonElement summary)
    {
        return summary.TryGetProperty("percentCompleted", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetDouble() : 0;
    }

    public async Task<StoryText> Story(int story, CancellationToken ct = default)
    {
        var body = await gh.Get($"repos/{repo}/issues/{story}", ct);
        return new StoryText(body.GetProperty("title").GetString() ?? "",
            body.TryGetProperty("body", out var b) && b.ValueKind == JsonValueKind.String ? b.GetString()! : "",
            body.GetProperty("state").GetString() == "open");
    }

    const string IssueStageQuery = """
        query($o:String!,$n:String!,$i:Int!,$sf:String!){repository(owner:$o,name:$n){issue(number:$i){id projectItems(first:10){nodes{id project{number} fieldValueByName(name:$sf){... on ProjectV2ItemFieldSingleSelectValue{name}}}}}}}
        """;

    const string ProjectQuery = """
        query($o:String!,$p:Int!,$sf:String!){repositoryOwner(login:$o){... on Organization{projectV2(number:$p){id field(name:$sf){... on ProjectV2SingleSelectField{id options{id name}}}}} ... on User{projectV2(number:$p){id field(name:$sf){... on ProjectV2SingleSelectField{id options{id name}}}}}}}
        """;

    // The project's id, its status field's id and each column's option id: read once, they do not move.
    (string Project, string Field, IReadOnlyDictionary<string, string> Options)? _project;

    async Task<(string Project, string Field, IReadOnlyDictionary<string, string> Options)> ProjectIds(int number, CancellationToken ct)
    {
        if (_project is { } known) return known;
        var data = await gh.GraphQl(ProjectQuery, new { o = Owner, p = number, sf = options.StatusField }, ct);
        var project = data.GetProperty("repositoryOwner").GetProperty("projectV2");
        var field = project.GetProperty("field");
        if (field.ValueKind != JsonValueKind.Object) throw new InvalidOperationException($"project {number} of {Owner} has no single-select field {options.StatusField}");
        var ids = (project.GetProperty("id").GetString()!, field.GetProperty("id").GetString()!,
            (IReadOnlyDictionary<string, string>)field.GetProperty("options").EnumerateArray().ToDictionary(o => o.GetProperty("name").GetString()!, o => o.GetProperty("id").GetString()!));
        _project = ids;
        return ids;
    }

    public async Task<string> StageOf(int story, CancellationToken ct = default)
    {
        var data = await gh.GraphQl(IssueStageQuery, new { o = Owner, n = RepoName, i = story, sf = options.StatusField }, ct);
        return StageIn(data.GetProperty("repository").GetProperty("issue"), options.Project);
    }

    // The story's column; the issue joins the project if it is not in it yet.
    public async Task SetStage(int story, string stage, CancellationToken ct = default)
    {
        var number = options.Project;
        var ids = await ProjectIds(number, ct);
        if (!ids.Options.TryGetValue(stage, out var option))
        {
            throw new InvalidOperationException($"project {number}'s {options.StatusField} has no column {stage} ({string.Join(", ", ids.Options.Keys)})");
        }
        var issue = (await gh.GraphQl(IssueStageQuery, new { o = Owner, n = RepoName, i = story, sf = options.StatusField }, ct)).GetProperty("repository").GetProperty("issue");
        var item = issue.GetProperty("projectItems").GetProperty("nodes").EnumerateArray()
            .Where(i => i.GetProperty("project").GetProperty("number").GetInt32() == number)
            .Select(i => i.GetProperty("id").GetString())
            .FirstOrDefault();
        if (item is null)
        {
            var added = await gh.GraphQl("mutation($p:ID!,$c:ID!){addProjectV2ItemById(input:{projectId:$p,contentId:$c}){item{id}}}",
                new { p = ids.Project, c = issue.GetProperty("id").GetString() }, ct);
            item = added.GetProperty("addProjectV2ItemById").GetProperty("item").GetProperty("id").GetString();
        }
        await gh.GraphQl("mutation($p:ID!,$i:ID!,$f:ID!,$o:String!){updateProjectV2ItemFieldValue(input:{projectId:$p,itemId:$i,fieldId:$f,value:{singleSelectOptionId:$o}}){projectV2Item{id}}}",
            new { p = ids.Project, i = item, f = ids.Field, o = option }, ct);
    }

    // GitHub's own attachments, the ones a pasted file becomes: uploaded to the repository's id.
    public async Task<Uri?> Attach(int story, string name, Stream content, string contentType, CancellationToken ct = default)
    {
        _repositoryId ??= (await gh.Get($"repos/{repo}", ct)).GetProperty("id").GetInt64();
        var address = $"https://uploads.github.com/user-attachments/assets?content_type={Uri.EscapeDataString(contentType)}&name={Uri.EscapeDataString(name)}&repository_id={_repositoryId}";
        var uploaded = await gh.Upload(address, content, contentType, ct);
        return uploaded.TryGetProperty("url", out var url) && url.GetString() is { } u ? new Uri(u) : null;
    }

    long? _repositoryId;

    public async Task<bool> TrySetStage(int story, string stage, ILogger log, CancellationToken ct = default)
    {
        try
        {
            await SetStage(story, stage, ct);
            return true;
        }
        catch (GitHubException e)
        {
            log.LogWarning("#{Issue}: stage write ({Stage}) failed: {Error}", story, stage, e.Message);
            return false;
        }
    }

    public async Task<IReadOnlySet<string>> Labels(int story, CancellationToken ct = default)
    {
        var body = await gh.Get($"repos/{repo}/issues/{story}", ct);
        return body.GetProperty("labels").EnumerateArray().Select(l => l.GetProperty("name").GetString()!).ToHashSet();
    }

    public async Task AddLabels(int story, params string[] labels)
    {
        await gh.Send(HttpMethod.Post, $"repos/{repo}/issues/{story}/labels", new { labels });
    }

    public async Task RemoveLabel(int story, string label)
    {
        try
        {
            await gh.Send(HttpMethod.Delete, $"repos/{repo}/issues/{story}/labels/{Uri.EscapeDataString(label)}");
        }
        catch (GitHubException e) when (e.HttpStatus == HttpStatusCode.NotFound)
        {
        }
    }

    public async Task<IReadOnlyList<string>> KnownLabels(CancellationToken ct = default)
    {
        var all = await gh.GetAll($"repos/{repo}/labels?per_page=100", ct: ct);
        return all.Select(l => l.GetProperty("name").GetString()!).ToList();
    }

    public async Task Comment(int story, string body)
    {
        await gh.Send(HttpMethod.Post, $"repos/{repo}/issues/{story}/comments", new { body });
    }

    public async Task<IReadOnlyList<StoryComment>> Comments(int story, CancellationToken ct = default)
    {
        var all = await gh.GetAll($"repos/{repo}/issues/{story}/comments?per_page=100", ct: ct);
        return all.Select(c => new StoryComment(c.GetProperty("created_at").GetDateTimeOffset(), c.GetProperty("body").GetString() ?? "",
            c.GetProperty("user").GetProperty("login").GetString() ?? "")).ToList();
    }

    public async Task<IReadOnlyDictionary<string, DateTimeOffset>> LabeledAt(int story, IReadOnlyCollection<string> labels, CancellationToken ct = default)
    {
        var events = await gh.GetAll($"repos/{repo}/issues/{story}/events?per_page=100", ct: ct);
        return events
            .Where(e => e.GetProperty("event").GetString() == "labeled"
                && e.TryGetProperty("label", out var l) && labels.Contains(l.GetProperty("name").GetString()!))
            .GroupBy(e => e.GetProperty("label").GetProperty("name").GetString()!)
            .ToDictionary(g => g.Key, g => g.Max(e => e.GetProperty("created_at").GetDateTimeOffset()));
    }

    public async Task<DateTimeOffset?> LastLift(int story, IReadOnlyCollection<string> labels, CancellationToken ct = default)
    {
        var events = await gh.GetAll($"repos/{repo}/issues/{story}/events?per_page=100", ct: ct);
        var lifts = events.Where(e => e.GetProperty("event").GetString() == "unlabeled"
                && e.TryGetProperty("label", out var l) && labels.Contains(l.GetProperty("name").GetString()!))
            .Select(e => e.GetProperty("created_at").GetDateTimeOffset()).ToList();
        return lifts.Count == 0 ? null : lifts.Max();
    }

    public async Task UpdateBody(int story, string body, CancellationToken ct = default)
    {
        await gh.Send(HttpMethod.Patch, $"repos/{repo}/issues/{story}", new { body }, ct);
    }

    public async Task Close(int story, CancellationToken ct = default)
    {
        await gh.Send(HttpMethod.Patch, $"repos/{repo}/issues/{story}", new { state = "closed" }, ct);
    }

    public async Task<int> CreateStory(string title, string body, CancellationToken ct = default)
    {
        var created = await gh.Send(HttpMethod.Post, $"repos/{repo}/issues", new { title, body }, ct);
        return created.GetProperty("number").GetInt32();
    }

    public async Task<int?> FindOpen(string title, bool exact = true, CancellationToken ct = default)
    {
        var q = Uri.EscapeDataString($"repo:{repo} is:issue is:open in:title \"{title}\"");
        var found = await gh.Get($"search/issues?q={q}&per_page=30&sort=created&order=asc", ct);
        return found.GetProperty("items").EnumerateArray()
            .Where(i => exact ? i.GetProperty("title").GetString() == title : (i.GetProperty("title").GetString() ?? "").StartsWith(title, StringComparison.Ordinal))
            .Select(i => (int?)i.GetProperty("number").GetInt32())
            .FirstOrDefault();
    }

    public async Task<int> Children(int story, CancellationToken ct = default)
    {
        var data = await gh.GraphQl("query($o:String!,$r:String!,$n:Int!){repository(owner:$o,name:$r){issue(number:$n){subIssuesSummary{total}}}}", new { o = Owner, r = RepoName, n = story }, ct);
        return data.GetProperty("repository").GetProperty("issue").GetProperty("subIssuesSummary").GetProperty("total").GetInt32();
    }

    public async Task<bool> VersionClosed(string version, CancellationToken ct = default)
    {
        return await Milestone(version, ct) is { Closed: true };
    }

    public async Task SetVersion(int story, string version, CancellationToken ct = default)
    {
        var number = (await Milestone(version, ct))?.Number ?? await OpenMilestone(version, ct);
        await gh.Send(HttpMethod.Patch, $"repos/{repo}/issues/{story}", new { milestone = number }, ct);
    }

    public async Task CloseVersion(string version, CancellationToken ct = default)
    {
        var number = (await Milestone(version, ct))?.Number ?? await OpenMilestone(version, ct);
        await gh.Send(HttpMethod.Patch, $"repos/{repo}/milestones/{number}", new { state = "closed" }, ct);
    }

    // The milestone titled exactly so: the search matches words, so its hits are filtered by title.
    async Task<(int Number, bool Closed)?> Milestone(string title, CancellationToken ct)
    {
        var data = await gh.GraphQl("query($o:String!,$r:String!,$t:String!){repository(owner:$o,name:$r){milestones(first:20,query:$t,states:[OPEN,CLOSED]){nodes{number title state}}}}",
            new { o = Owner, r = RepoName, t = title }, ct);
        foreach (var m in data.GetProperty("repository").GetProperty("milestones").GetProperty("nodes").EnumerateArray())
        {
            if (m.GetProperty("title").GetString() == title) return (m.GetProperty("number").GetInt32(), m.GetProperty("state").GetString() == "CLOSED");
        }
        return null;
    }

    async Task<int> OpenMilestone(string title, CancellationToken ct)
    {
        var created = await gh.Send(HttpMethod.Post, $"repos/{repo}/milestones", new { title }, ct);
        return created.GetProperty("number").GetInt32();
    }
}
