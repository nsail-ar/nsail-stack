// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using System.Text;
using System.Text.Json;
using NSail.Harness.Agents;
using NSail.Harness.Lanes;
using NSail.Harness.Planning;
using NSail.Harness.Workers;

namespace NSail.Harness.Jobs;

// A QA walk of one Released story in a fresh browser on its product's test tenant. The harness lays
// down what the walker needs (the story, its PRs and files, what QA already asked, and which build
// each tenant runs) and the Test worker's seat walks it through the worker's MCP servers, writing its
// verdict — `VERDICT: <word>` on the first line, the report under it. A story whose code its tenant
// does not run yet is not walked. The verdict is the job's event; what each does on the board is the
// worker's (WorkerTypes). A worker that names a Command instead runs it, and its command writes the
// verdict to qa/verdicts/<story>.md under HARNESS_TEMP.
public sealed class QaJob(WorkItem item, JobContext ctx, JobHistory history) : IJob
{
    public WorkItem Item { get; } = item;

    const string Out = ".qa";

    const string NoBrowser = "is not installed; expected executable at";

    public async Task<JobOutcome> Run(int slot, string logPath, CancellationToken ct)
    {
        // The board may have moved since the plan: a story no longer where a walk takes it is left.
        if (Item.Story is { } n && await ctx.Board.StageOf(n, ct) is var stage && stage is not (Stages.Released or Stages.Qa))
        {
            return new JobOutcome($"in {stage} now — not walked", Idle: true);
        }
        var step = ctx.StepOf(Item);
        return step.Command is null ? await Walk(Item.Story!.Value, slot, logPath, step, ct) : await Script(slot, logPath, step, ct);
    }

    async Task<JobOutcome> Walk(int n, int slot, string logPath, JobStep step, CancellationToken ct)
    {
        var started = DateTimeOffset.UtcNow;
        if (await Quiet(n, step, started, ct)) return new JobOutcome("waiting on its ask, nothing new since its last walk", Idle: true);
        var dir = await ctx.Workspace.Prepare(slot);
        await using var w = new StreamWriter(logPath, append: true) { AutoFlush = true };
        await w.WriteLineAsync($"== walk {ctx.Names.Ref(n)} on {ctx.Options.Machine} slot {slot}");
        await Shell.Git(dir, [.. ctx.Workspace.Guard, "fetch", "-q", "--force", ctx.Workspace.Url, "+refs/tags/*:refs/tags/*"]);

        var labels = await ctx.Board.Labels(n, ct);
        var product = ProductOf(labels, ctx.Options.Products);
        if (product is null) return new JobOutcome("no product to walk it on (Harness:Products)", Idle: true);
        var merge = (await ctx.Repo.MergeOf(n, ct))?.Sha;
        var live = new List<(string Key, ProductOptions Product, string? Tag, string Answer)>();
        foreach (var (key, p) in ctx.Options.Products)
        {
            var tag = await LiveTag(dir, p, ct);
            live.Add((key, p, tag, await LiveIn(dir, merge, tag)));
        }
        var own = live.Single(l => l.Product == product);
        if (own.Answer == "no" || (own.Answer == "unknown" && product.Live.Header is not null))
        {
            await w.WriteLineAsync($"{ctx.Names.Ref(n)} — not live on {product.Name} yet (it runs {own.Tag ?? "nothing it says"}), not walked");
            return new JobOutcome($"walked: NotLive", Event: "NotLive", Report: "");
        }

        // A walk's out folder is its own: an earlier walk's shots would be attached as this one's.
        var outDir = Path.Combine(dir, Out);
        if (Directory.Exists(outDir)) Directory.Delete(outDir, recursive: true);
        var shots = Path.Combine(outDir, "shots");
        Directory.CreateDirectory(shots);
        var story = await ctx.Board.Story(n, ct);
        await File.WriteAllTextAsync(Path.Combine(outDir, "story.md"), $"{StoryDossier.Heading(ctx.Names.Ref(n), story.Title)}\n\n{story.Body}", ct);
        var dossier = new StoryDossier(ctx, history);
        await dossier.WritePrs(n, outDir, ct);
        var asked = labels.Contains(Labels.QaAsked) ? Asked(await ctx.Board.Comments(n, ct), await dossier.Trusted(ct)) : "";
        await File.WriteAllTextAsync(Path.Combine(outDir, "asked.md"), asked, ct);

        var env = await ctx.LaneEnv(started.AddMinutes(step.Minutes), Item, step, ct);
        await ctx.Prepare(dir, env, w);
        var mcp = Path.Combine(outDir, "mcp.json");
        await File.WriteAllTextAsync(mcp, McpConfig(step.Worker.Mcp, shots, ctx.Options.Products.Values.Select(p => p.Tenant)), ct);

        var tenants = string.Join('\n', ctx.Options.Products.Values.Select(p =>
            $"{p.Name}: {p.Tenant} — user `{Value(env, p.UserVariable)}`, password `{Value(env, p.PasswordVariable)}`"));
        var lives = string.Join('\n', live.Select(l => $"{l.Product.Name} test runs {l.Tag ?? "unknown"}: this story is in it: {l.Answer}"));
        var seat = step.Agent ?? throw new InvalidOperationException($"{step.Worker} names no seat");
        var prompt = await SeatPrompt.Read(dir, seat, new Dictionary<string, string>
        {
            ["ISSUE"] = n.ToString(CultureInfo.InvariantCulture),
            ["STORY"] = ctx.Names.Ref(n),
            ["OUT"] = Out,
            ["PRODUCT"] = product.Name,
            ["TENANTS"] = tenants,
            ["LIVE"] = lives,
            ["ASKED"] = asked.Length > 0 ? "yes" : "no",
            ["TREE"] = dir,
        }, ct);

        var tools = step.Worker.Mcp.Keys.Select(k => $"mcp__{k}__*").Concat(step.Worker.Tools).ToList();
        // The agent opens this same log to append to it, and a second writer is refused while this one is open.
        await w.DisposeAsync();
        var result = await ctx.RunSeat(new AgentRun(
            prompt, dir, ctx.ModelOf(labels, seat),
            Access: AgentAccess.Walker,
            MaxTurns: 200,
            Timeout: TimeSpan.FromMinutes(step.Minutes),
            Env: ctx.SeatEnv(env),
            LogPath: logPath,
            Marketplace: dir,
            Mcp: mcp,
            Tools: tools), ct);
        // A walker whose browser is not there still writes a verdict from the story alone: the walk is void, and loud.
        if (File.Exists(logPath) && (await File.ReadAllTextAsync(logPath, ct)).Contains(NoBrowser, StringComparison.Ordinal))
        {
            await File.AppendAllTextAsync(logPath, $"{ctx.Names.Ref(n)} — the walker had no browser: walk void{Environment.NewLine}", ct);
            return new JobOutcome("walk void: the walker's browser is not installed on this machine (npx @playwright/mcp install-browser chromium)", Event: "NoVerdict", Failed: true, Report: "");
        }
        var quota = result.QuotaRefused ? result.QuotaUntil ?? DateTimeOffset.UtcNow.AddHours(1) : (DateTimeOffset?)null;

        // A walker that answered its report instead of writing it: its final message is the report.
        var reportFile = Path.Combine(outDir, "report.md");
        var text = File.Exists(reportFile) ? await File.ReadAllTextAsync(reportFile, ct) : FromVerdict(result.Result);
        var why = text.Length > 0 ? "" : result.TimedOut ? "El walker no terminó a tiempo." : $"El walker terminó sin reporte (exit {result.ExitCode}).";
        var (evt, report) = Verdict(text.Length > 0 ? text : why);
        // A story waiting on a person is asked once: a walker that asks again, or doubts again, is still waiting.
        if (evt is "Asked" or "Doubt" && asked.Length > 0)
        {
            await File.AppendAllTextAsync(logPath, $"{ctx.Names.Ref(n)} — {evt} again while waiting on the first ask: waiting{Environment.NewLine}", ct);
            return new JobOutcome("walked: Waiting", quota, Event: "Waiting", Report: "");
        }
        report = $"{report}\n\n{await Evidence(n, dir, shots, started, ct)}\n\nLog: {Path.GetFileName(logPath)} on {ctx.Options.Machine}".Trim();
        if (quota is not null && evt == "NoVerdict") return new JobOutcome("walk stopped at the quota wall", quota);
        if (evt is "Bug" && await BugBefore(n, ct)) evt = "BugAgain";
        return new JobOutcome($"walked: {evt}", quota, Event: evt, Failed: evt == "NoVerdict", Report: report);
    }

    // A story waiting on its ask that this machine walked within the worker's Recheck, with no
    // comment since: the walk would only read the same silence again.
    async Task<bool> Quiet(int n, JobStep step, DateTimeOffset now, CancellationToken ct)
    {
        if (!(await ctx.Board.Labels(n, ct)).Contains(Labels.QaAsked)) return false;
        var last = history.Jobs()
            .Where(j => j.Story == n && j.Ended is not null && j.Outcome?.StartsWith("walked", StringComparison.Ordinal) == true)
            .MaxBy(j => j.Ended);
        if (last?.Ended is not { } ended || now - ended >= TimeSpan.FromMinutes(step.Worker.Recheck ?? 360)) return false;
        // The ask itself is posted after its walk ends: it is not news.
        return !(await ctx.Board.Comments(n, ct)).Any(c => c.At > ended && !c.Body.Contains("<!-- qa-asked:", StringComparison.Ordinal));
    }

    static readonly Dictionary<string, string> Media =new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".gif"] = "image/gif", [".webp"] = "image/webp",
        [".webm"] = "video/webm", [".mp4"] = "video/mp4",
    };

    // The walk's shots attached to the board, in their names' order, as the comment shows them; a
    // board that keeps no attachments, or a shot it refused, is named by its path instead.
    async Task<string> Evidence(int n, string dir, string shots, DateTimeOffset started, CancellationToken ct)
    {
        // Every image or video under the out folder, and any the walk left at the tree's root: the
        // browser resolves a bare file name against its working directory, not its output folder.
        var files = Directory.EnumerateFiles(Path.GetDirectoryName(shots)!, "*", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(dir).Where(f => File.GetLastWriteTimeUtc(f) >= started.UtcDateTime))
            .Where(f => Media.ContainsKey(Path.GetExtension(f)))
            .OrderBy(Path.GetFileName, StringComparer.Ordinal).Take(20).ToList();
        if (files.Count == 0) return "";
        var lines = new List<string> { "**Capturas**", "" };
        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            Uri? url = null;
            try
            {
                await using var content = File.OpenRead(file);
                url = await ctx.Board.Attach(n, name, content, Media[Path.GetExtension(file)], ct);
            }
            catch (Exception e) when (e is IOException or ProviderException or HttpRequestException)
            {
            }
            var video = Media[Path.GetExtension(file)].StartsWith("video/", StringComparison.Ordinal);
            lines.Add(url is null ? $"- `{file}`" : video ? url.ToString() : $"![{Path.GetFileNameWithoutExtension(name)}]({url})");
            lines.Add("");
        }
        return string.Join('\n', lines).TrimEnd();
    }

    // The product a story is walked on: the one its labels name, else the default.
    // A walk that found the tenant not running the story yet is asked again on a short clock, not
    // at the next cut: a deploy lands after its cut (a ring follows the registry, a hand installs),
    // so the cut that made the story live has always already passed when the tenant takes it.
    public static readonly TimeSpan NotLiveRecheck = TimeSpan.FromMinutes(15);

    // Whether a walk keeps its story until the next cut: every verdict but NotLive does.
    internal static bool WaitsForTheCut(JobOutcome outcome)
    {
        return outcome.Event != "NotLive";
    }

    internal static ProductOptions? ProductOf(IReadOnlySet<string> labels, IReadOnlyDictionary<string, ProductOptions> products)
    {
        return products.Values.FirstOrDefault(p => p.Labels.Any(labels.Contains)) ?? products.Values.FirstOrDefault(p => p.Default);
    }

    // The tag naming the build a tenant runs: the newest of its prefix, or the one its header names.
    async Task<string?> LiveTag(string dir, ProductOptions p, CancellationToken ct)
    {
        if (p.Live.Header is { } header)
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            try
            {
                using var response = await http.GetAsync(p.Live.Url ?? p.Tenant, HttpCompletionOption.ResponseHeadersRead, ct);
                var build = response.Headers.TryGetValues(header, out var values) ? values.FirstOrDefault() : null;
                return BuildRef(build, p.Live.Tag);
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
            {
                return null;
            }
        }
        var tags = await Shell.Run("git", ["tag", "-l", $"{p.Live.Tag}*", "--sort=-v:refname"], dir, ct: ct);
        return tags.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
    }

    // What a build a tenant names is in git: the commit its build metadata carries (`1.0.0+<sha>`),
    // else its version's tag; none for a local build.
    internal static string? BuildRef(string? build, string tagPrefix)
    {
        if (string.IsNullOrWhiteSpace(build) || build == "dev") return null;
        var plus = build.IndexOf('+');
        return plus >= 0 && build.Length > plus + 7 ? build[(plus + 1)..] : $"{tagPrefix}{build}";
    }

    // "yes" when the story's last merge is inside the tag, "no" when it is not, "unknown" when either
    // is missing (no merge here: a story built in another repository).
    static async Task<string> LiveIn(string dir, string? merge, string? tag)
    {
        if (merge is null || tag is null) return "unknown";
        var r = await Shell.Run("git", ["merge-base", "--is-ancestor", merge, $"{tag}^{{commit}}"], dir);
        return r.ExitCode switch { 0 => "yes", 1 => "no", _ => "unknown" };
    }

    // What QA asked a person, and every comment since: the walk only looks for the answer.
    internal static string Asked(IReadOnlyList<Board.StoryComment> comments, ISet<string> trusted)
    {
        var ordered = comments.OrderBy(c => c.At).ToList();
        var at = ordered.FindLastIndex(c => c.Body.Contains("<!-- qa-asked:", StringComparison.Ordinal));
        return at < 0 ? "" : StoryDossier.Thread(ordered.Skip(at), trusted);
    }

    static string Value(IReadOnlyDictionary<string, string> env, string? name)
    {
        return name is not null && env.TryGetValue(name, out var v) ? v : "";
    }

    // The worker's MCP servers in the CLI's file, each told where its screenshots go and which origins
    // it may open; `{shots}` and `{origins}` in an argument are those.
    internal static string McpConfig(IReadOnlyDictionary<string, CommandOptions> servers, string shots, IEnumerable<string> tenants)
    {
        var origins = string.Join(';', tenants.Where(t => t.Length > 0));
        var named = servers.ToDictionary(s => s.Key, s => new
        {
            command = s.Value.Run,
            args = s.Value.Args.Select(a => a.Replace("{shots}", shots, StringComparison.Ordinal).Replace("{origins}", origins, StringComparison.Ordinal)).ToArray(),
        });
        return JsonSerializer.Serialize(new { mcpServers = named });
    }

    static string FromVerdict(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var at = text.IndexOf("VERDICT:", StringComparison.Ordinal);
        return at < 0 ? "" : text[at..];
    }

    async Task<bool> BugBefore(int n, CancellationToken ct)
    {
        var since = await ctx.Board.LastLift(n, Labels.Lifts, ct);
        return (await ctx.Board.Comments(n, ct)).Any(c => (since is null || c.At > since) && c.Body.Contains(WorkerTypes.QaBugMarker, StringComparison.Ordinal));
    }

    async Task<JobOutcome> Script(int slot, string logPath, JobStep step, CancellationToken ct)
    {
        var dir = await ctx.Workspace.Prepare(slot);
        // Each slot walks in its own corner — run folder, tools, browser port and CLI config — so
        // walks side by side on one machine never touch each other's.
        var root = Path.Combine(ctx.Options.WorkRoot, "qa");
        var temp = Path.Combine(root, $"run-{slot}");
        if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true);
        Directory.CreateDirectory(temp);

        var env = await ctx.LaneEnv(DateTimeOffset.UtcNow.AddMinutes(step.Minutes), Item, step, ct);
        env["HARNESS_WORKSPACE"] = dir;
        env["HARNESS_TEMP"] = temp;
        if (Item.Story is { } asked) env["ASKED"] = asked.ToString(CultureInfo.InvariantCulture);
        // The browser tools persist between walks: an install is minutes, a reuse nothing.
        env["PW"] = Path.Combine(root, $"tools-{slot}");
        env["PORT"] = (9222 + slot).ToString(CultureInfo.InvariantCulture);
        env["RUN"] = $"{ctx.Options.PublicUrl} — log {Path.GetFileName(logPath)} on {ctx.Options.Machine}";
        foreach (var (name, value) in ctx.Agent.ScriptEnvironment($"qa-{slot}")) env[name] = value;

        await using var w = new StreamWriter(logPath, append: true) { AutoFlush = true };
        await w.WriteLineAsync($"== {Item} on {ctx.Options.Machine} slot {slot}");
        var result = await ctx.Run(step.Command!, [], dir, env, TimeSpan.FromMinutes(step.Minutes), w, ct);
        var (refused, until) = ctx.Agent.Quota(result.Output);
        var quota = refused ? until ?? DateTimeOffset.UtcNow.AddHours(1) : (DateTimeOffset?)null;
        var ended = result.ExitCode == 0 ? "walked" : $"walk ended {(result.TimedOut ? "by timeout" : $"with exit {result.ExitCode}")}";
        var file = Item.Story is { } s ? Path.Combine(temp, "qa", "verdicts", $"{s}.md") : null;
        if (file is null || !File.Exists(file)) return new JobOutcome(ended, quota, Failed: result.ExitCode != 0);

        var (evt, report) = Verdict(await File.ReadAllTextAsync(file, ct));
        if (evt is "Bug" && Item.Story is { } n2 && await BugBefore(n2, ct)) evt = "BugAgain";
        return new JobOutcome($"{ended}: {evt}", quota, Event: evt, Failed: evt == "NoVerdict", Report: report);
    }

    // The event a verdict file names, and the report under its first line.
    public static (string Event, string Report) Verdict(string text)
    {
        var lines = text.Replace("\r", "", StringComparison.Ordinal).Split('\n');
        var word = lines[0].StartsWith("VERDICT:", StringComparison.Ordinal) ? lines[0]["VERDICT:".Length..].Trim() : "";
        var evt = word switch
        {
            "ok" => "Ok",
            "n/a" => "NotApplicable",
            "bug" => "Bug",
            "asked" => "Asked",
            "waiting" => "Waiting",
            "not-live" => "NotLive",
            "doubt" => "Doubt",
            _ => "NoVerdict",
        };
        var body = word.Length > 0 ? lines[1..] : lines;
        // `ASKED: story|product` is the walker telling how it asked: the reader is the one asked.
        if (evt == "Asked" && body.FirstOrDefault(l => l.Trim().Length > 0) is { } first && first.StartsWith("ASKED:", StringComparison.Ordinal))
            body = [.. body.SkipWhile(l => l != first).Skip(1)];
        return (evt, string.Join('\n', body).Trim());
    }
}
