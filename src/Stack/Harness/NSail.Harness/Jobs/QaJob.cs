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
// down what the walker needs (the story with its whole thread, its PRs and files, and which build each tenant runs) and the Test worker's seat walks it through the worker's MCP servers, writing its
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
        if ((await ctx.Board.Labels(n, ct)).Contains(Labels.NoWalk))
        {
            return new JobOutcome("not walked: no-walk", Event: "NotApplicable", Report: $"{ctx.Texts.NoWalk} {ctx.Texts.Signature}");
        }
        var dir = await ctx.Workspace.Prepare(slot);
        await using var w = new StreamWriter(logPath, append: true) { AutoFlush = true };
        await w.WriteLineAsync($"== walk {ctx.Names.Ref(n)} on {ctx.Options.Machine} slot {slot}");
        await ctx.Workspace.Fetch(dir, prune: false, "+refs/tags/*:refs/tags/*");

        var labels = await ctx.Board.Labels(n, ct);
        var product = ProductOf(labels, ctx.Options.Products);
        if (product is null) return new JobOutcome("no product to walk it on (Harness:Products)", Idle: true);
        var merge = (await ctx.LandingOf(n, ct))?.Sha;
        var live = new List<(string Key, ProductOptions Product, string? Tag, string Answer)>();
        foreach (var (key, p) in ctx.Options.Products)
        {
            var tag = await LiveTag(dir, p, ct);
            live.Add((key, p, tag, await LiveIn(dir, merge, tag)));
        }
        // A story of two products is live when both run it: the walker would find the other one missing and spend the walk saying so.
        var behind = live.Where(l => l.Product == product || l.Product.Labels.Any(labels.Contains))
            .FirstOrDefault(l => l.Answer == "no" || (l.Answer == "unknown" && l.Product.Live.Header is not null));
        if (behind.Product is not null)
        {
            await w.WriteLineAsync($"{ctx.Names.Ref(n)} — not live on {behind.Product.Name} yet (it runs {behind.Tag ?? "nothing it says"}), not walked");
            return new JobOutcome($"walked: NotLive", Event: "NotLive", Report: "");
        }
        // The check above reads the product the board names; a walker goes where the story's thread sends it, and
        // one that found that tenant behind said so of these builds. Until one of them changes the answer is the same.
        var builds = string.Join(' ', live.Select(l => l.Tag ?? "-"));
        if (StoodNotLive(history.Jobs(), n, builds))
        {
            await w.WriteLineAsync($"{ctx.Names.Ref(n)} — a walker found it not live on these builds ({builds}) and none has changed, not walked");
            return new JobOutcome($"walked: NotLive", Event: "NotLive", Report: "");
        }

        // The QA column is what is being walked: the story enters it here and leaves it by its verdict. A walk that
        // gave none (a doubt, a dead walker, the quota wall) puts it back where it waits its turn.
        await ctx.Board.SetStage(n, Stages.Qa, ct);
        JobOutcome? ended = null;
        try
        {
            ended = await Seated();
            return ended;
        }
        finally
        {
            if (!LeavesQa(ended))
            {
                await ctx.Board.TrySetStage(n, Stages.Released, ctx.Logs.CreateLogger<QaJob>(), CancellationToken.None);
            }
        }

        async Task<JobOutcome> Seated()
        {
            // A walk's out folder is its own: an earlier walk's shots would be attached as this one's.
            var outDir = Path.Combine(dir, Out);
            await ctx.Remove(outDir, ct);
            var shots = Path.Combine(outDir, "shots");
            Directory.CreateDirectory(shots);
            var dossier = new StoryDossier(ctx, history);
            await dossier.WriteStory(n, outDir, ct);
            await dossier.WritePrs(n, outDir, ct);

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
            if (File.Exists(logPath) && Browserless(await File.ReadAllTextAsync(logPath, ct), step.Worker.Mcp.Keys))
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
            report = $"{report}\n\n{await Evidence(n, dir, shots, started, ct)}\n\nLog: {Path.GetFileName(logPath)} on {ctx.Options.Machine}".Trim();
            if (quota is not null && evt == "NoVerdict") return new JobOutcome("walk stopped at the quota wall", quota);
            if (evt is "Bug" && await Before(n, WorkerTypes.QaBugMarker, ct)) evt = "BugAgain";
            if (evt is "Doubt" && await Before(n, WorkerTypes.QaDoubtMarker, ct)) evt = "DoubtAgain";
            return new JobOutcome(evt == "NotLive" ? $"{Walked}NotLive{At}{builds}" : $"{Walked}{evt}", quota, Event: evt, Failed: evt == "NoVerdict", Report: report);
        }
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

    // Whether a browser tool's own result says its browser is not installed. Only the stream's objects
    // are read — a result tied through its tool_use_id to a `mcp__<server>__` call, whatever its
    // is_error says — since the thread the walker reads can quote an earlier walk's failure.
    internal static bool Browserless(string log, IEnumerable<string> servers)
    {
        var prefixes = servers.Select(k => $"mcp__{k}__").ToList();
        var browser = new HashSet<string>(StringComparer.Ordinal);
        var failed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in log.Split('\n'))
        {
            var t = line.Trim();
            if (t.Length == 0 || t[0] != '{') continue;
            JsonElement o;
            try
            {
                using var doc = JsonDocument.Parse(t);
                o = doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                continue;
            }
            if (o.ValueKind != JsonValueKind.Object || !o.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object) continue;
            if (!message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array) continue;
            foreach (var c in content.EnumerateArray())
            {
                if (c.ValueKind != JsonValueKind.Object) continue;
                var kind = Str(c, "type");
                if (kind == "tool_use" && Str(c, "id") is { } id && Str(c, "name") is { } name && prefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal)))
                {
                    browser.Add(id);
                }
                if (kind == "tool_result" && Str(c, "tool_use_id") is { } answers && c.TryGetProperty("content", out var result) && Strings(result).Any(s => s.Contains(NoBrowser, StringComparison.Ordinal)))
                {
                    failed.Add(answers);
                }
            }
        }
        return failed.Overlaps(browser);
    }

    static string? Str(JsonElement o, string name)
    {
        return o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    static IEnumerable<string> Strings(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.String)
        {
            yield return e.GetString()!;
        }
        else if (e.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in e.EnumerateArray().SelectMany(Strings)) yield return s;
        }
        else if (e.ValueKind == JsonValueKind.Object)
        {
            foreach (var s in e.EnumerateObject().SelectMany(p => Strings(p.Value))) yield return s;
        }
    }

    // The product a story is walked on: the one its labels name, else the default.
    // A walk that found the tenant not running the story yet is asked again on a short clock, not
    // at the next cut: a deploy lands after its cut (a ring follows the registry, a hand installs),
    // so the cut that made the story live has always already passed when the tenant takes it.
    public static readonly TimeSpan NotLiveRecheck = TimeSpan.FromMinutes(15);

    // A walk that could not be made (the tenant down, the tools failed) is tried again on its own,
    // longer clock: each try spends a walker.
    public static readonly TimeSpan DoubtRecheck = TimeSpan.FromHours(1);

    const string Walked = "walked: ";

    // What follows a walker's own NotLive in its job's outcome: the builds it was said of.
    const string At = " at ";

    // Whether the last walk of the story a walker made here ended NotLive of exactly these builds.
    internal static bool StoodNotLive(IEnumerable<JobRecord> jobs, int story, string builds)
    {
        var last = jobs.Where(j => j.Story == story && j is { Seated: true, Ended: not null, Outcome: not null } && j.Outcome.StartsWith(Walked, StringComparison.Ordinal))
            .MaxBy(j => j.Ended);
        return last?.Outcome == $"{Walked}NotLive{At}{builds}";
    }

    // The event a walk's outcome names: its first word.
    static string EventOf(string outcome)
    {
        var word = outcome[Walked.Length..];
        var end = word.IndexOf(' ');
        return end < 0 ? word : word[..end];
    }

    // What this machine's own record says of its walks, for a harness that just started: the stories
    // it walked and when, and the ones a walk left to be tried again and until when. Without it a
    // restart forgets both clocks and walks everything again at once.
    internal static (Dictionary<int, DateTimeOffset> Walked, Dictionary<int, DateTimeOffset> Again) Remembered(IEnumerable<JobRecord> jobs)
    {
        var walked = new Dictionary<int, DateTimeOffset>();
        var again = new Dictionary<int, DateTimeOffset>();
        foreach (var job in jobs.Where(j => j is { Story: not null, Ended: not null, Outcome: not null } && j.Outcome.StartsWith(Walked, StringComparison.Ordinal)).OrderBy(j => j.Ended))
        {
            var n = job.Story!.Value;
            var outcome = new JobOutcome(job.Outcome!, Event: EventOf(job.Outcome!));
            walked.Remove(n);
            again.Remove(n);
            if (WaitsForTheCut(outcome))
            {
                walked[n] = job.Ended!.Value;
            }
            else
            {
                again[n] = job.Ended!.Value + RecheckOf(outcome);
            }
        }
        return (walked, again);
    }

    // Whether a walk keeps its story until the next cut: every verdict but NotLive and Doubt does.
    internal static bool WaitsForTheCut(JobOutcome outcome)
    {
        return outcome.Event is not ("NotLive" or "Doubt");
    }

    // Whether a walk's verdict moves its story out of QA by itself (WorkerTypes, Test): any other ending moves nothing.
    internal static bool LeavesQa(JobOutcome? outcome)
    {
        return outcome?.Event is "Ok" or "NotApplicable" or "Bug" or "BugAgain";
    }

    // The check before the walk writes no report; a walker that found its tenant behind wrote one, and spent a walk.
    internal static TimeSpan RecheckOf(JobOutcome outcome)
    {
        return outcome.Event == "Doubt" || outcome.Report is { Length: > 0 } ? DoubtRecheck : NotLiveRecheck;
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
    // is missing (no merge the default branch holds: a pinned repository's merge waits for its pin).
    static async Task<string> LiveIn(string dir, string? merge, string? tag)
    {
        if (merge is null || tag is null) return "unknown";
        var r = await Shell.Run("git", ["merge-base", "--is-ancestor", merge, $"{tag}^{{commit}}"], dir);
        return r.ExitCode switch { 0 => "yes", 1 => "no", _ => "unknown" };
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

    // Whether an earlier walk already said the same on this story, since a hand last lifted a hold.
    async Task<bool> Before(int n, string marker, CancellationToken ct)
    {
        var since = await ctx.Board.LastLift(n, Labels.Lifts, ct);
        var login = await ctx.Repo.Login(ct);
        return (await ctx.Board.Comments(n, ct)).Any(c => (since is null || c.At > since) && ctx.Markers.Says(c, marker, login));
    }

    async Task<JobOutcome> Script(int slot, string logPath, JobStep step, CancellationToken ct)
    {
        var dir = await ctx.Workspace.Prepare(slot);
        // Each slot walks in its own corner — run folder, tools, browser port and CLI config — so
        // walks side by side on one machine never touch each other's.
        var root = Path.Combine(ctx.Options.WorkRoot, "qa");
        var temp = Path.Combine(root, $"run-{slot}");
        await ctx.Remove(temp, ct);
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
        var result = await ctx.Run(step.Command!, [], dir, env, TimeSpan.FromMinutes(step.Minutes), w, ct, keep: ctx.Agent.IsSignal);
        var (refused, until) = ctx.Agent.Quota(result.Output);
        var quota = refused ? until ?? DateTimeOffset.UtcNow.AddHours(1) : (DateTimeOffset?)null;
        var ended = result.ExitCode == 0 ? "walked" : $"walk ended {(result.TimedOut ? "by timeout" : $"with exit {result.ExitCode}")}";
        var file = Item.Story is { } s ? Path.Combine(temp, "qa", "verdicts", $"{s}.md") : null;
        if (file is null || !File.Exists(file)) return new JobOutcome(ended, quota, Failed: result.ExitCode != 0);

        var (evt, report) = Verdict(await File.ReadAllTextAsync(file, ct));
        if (evt is "Bug" && Item.Story is { } n2 && await Before(n2, WorkerTypes.QaBugMarker, ct)) evt = "BugAgain";
        if (evt is "Doubt" && Item.Story is { } n3 && await Before(n3, WorkerTypes.QaDoubtMarker, ct)) evt = "DoubtAgain";
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
            "unwalkable" or "ruling" => "Unwalkable",
            "not-live" => "NotLive",
            "doubt" => "Doubt",
            _ => "NoVerdict",
        };
        var body = word.Length > 0 ? lines[1..] : lines;
        return (evt, string.Join('\n', body).Trim());
    }
}
