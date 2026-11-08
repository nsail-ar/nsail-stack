// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using NSail.Harness.Agents;
using NSail.Harness.Board;
using NSail.Harness.Builds;
using NSail.Harness.Lanes;
using NSail.Harness.Planning;
using NSail.Harness.Repositories;
using NSail.Harness.Workers;

namespace NSail.Harness.Jobs;

// What every job reads and writes through. One per harness.
public sealed class JobContext(
    IBoard board,
    RepositorySet repos,
    IReadOnlyDictionary<string, Workspace> workspaces,
    IAgent agent,
    HarnessOptions options,
    IEnumerable<ILaneEnvironment> environments,
    ILoggerFactory logs,
    BuildLeases leases)
{
    public IBoard Board { get; } = board;
    public RepositorySet Repos { get; } = repos;
    public IRepository Repo => Repos.Default;

    // The default repository's slots: where every seat starts, the gate builds and a release cuts.
    public Workspace Workspace => workspaces[Repo.Name];
    public IAgent Agent { get; } = agent;
    public HarnessOptions Options { get; } = options;
    public HarnessTexts Texts => Options.Texts;
    public StoryNames Names { get; } = options.Names();
    public Markers Markers { get; } = new(options.ReviewMarker);
    public ILoggerFactory Logs { get; } = logs;
    public BuildLeases Leases { get; } = leases;

    // The log sources a Watch worker reads, by Name: the host's providers (AddOpenObserve, ...).
    public IReadOnlyList<Watching.ILogSource> LogSources { get; init; } = [];

    // How the default repository builds and tests, when it says (Repositories[].Build).
    public IBuilder? Builder { get; } = Builders.For(options.DefaultRepository.Build, options.BuildNodes);

    // What a job runs: its worker's seat (with the worker's prompt layered after the seat's), command,
    // minutes (its type's when unset) and variables.
    public JobStep StepOf(WorkItem item)
    {
        if (item.Worker is not { } name || !Options.Workers.TryGetValue(name, out var w)) throw new InvalidOperationException($"{item} names no worker");
        var agent = w.Seat is { } seat ? Options.Seats[seat].Clone() : null;
        if (agent is not null)
        {
            agent.WorkerPrompt = w.Prompt;
            agent.WorkerPromptFile = w.PromptFile;
        }
        var command = w.Command is { } run ? new CommandOptions { Run = run, Args = w.Args } : null;
        return new JobStep(w, agent, command, w.Minutes ?? WorkerTypes.All[w.Type].Minutes, w.Variables);
    }

    // A seat's run, and the end of its reach: every clone it shared is rewritten as the harness wrote it
    // before any other git runs there. A restore that fails costs nothing here: the next Prepare redoes it,
    // and the work the seat left is still to be delivered.
    public async Task<AgentResult> RunSeat(AgentRun run, CancellationToken ct)
    {
        try
        {
            return await Agent.Run(run, ct);
        }
        finally
        {
            foreach (var w in workspaces.Values)
            {
                try
                {
                    await w.Restore();
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
                {
                }
            }
        }
    }

    // A repository's own slots: a story it holds is built and pushed there.
    public Workspace WorkspaceOf(IRepository repository)
    {
        return workspaces[repository.Name];
    }

    // The name a job's build turns are held under, so they all come back when the job ends.
    public static string HolderOf(WorkItem item)
    {
        return $"{item.Kind.ToString().ToLowerInvariant()}-{item.Story?.ToString(CultureInfo.InvariantCulture) ?? item.Product ?? (item.Kind == WorkKind.Main ? "head" : item.Kind == WorkKind.Watch ? item.Worker ?? "watch" : "drain")}";
    }

    public string LogPath(WorkItem item, DateTimeOffset at)
    {
        var name = $"{at:yyyyMMdd-HHmmss}-{HolderOf(item)}.log";
        var dir = Path.Combine(Options.WorkRoot, "logs");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, name);
    }

    // The environment every lane's tools see: the providers' credentials (a step that runs code, only if it declares them), the harness's build-turn
    // door, the clock, and the project's variables (HarnessOptions.Variables, then the worker's own).
    public async Task<Dictionary<string, string>> LaneEnv(DateTimeOffset axe, WorkItem item, JobStep? step = null, CancellationToken ct = default, bool runsCode = false)
    {
        var shims = Path.Combine(AppContext.BaseDirectory, "shims");
        var env = await VariableReferences.Resolve(Options.Variables.Concat(step?.Variables ?? new Dictionary<string, string>()), RepositoryVariable, ct);
        env["PATH"] = shims + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
        // The doorman first on the PATH: build-heavy commands wait for a turn (shims/).
        env["HARNESS_URL"] = Options.PublicUrl;
        env["HARNESS_HOLDER"] = HolderOf(item);
        env["HARNESS_BUILD_NODES"] = Options.BuildNodes.ToString(CultureInfo.InvariantCulture);
        env["HARNESS_RANK"] = Planner.Rank(item.Tier).ToString(CultureInfo.InvariantCulture);
        // The compiler server and MSBuild nodes outlive a build by design and hold gigabytes: a lane
        // that builds a few times an hour is better off paying the startup than keeping them.
        env["UseSharedCompilation"] = "false";
        env["MSBUILDDISABLENODEREUSE"] = "1";
        // What every CI sets, and what tests read to widen their waits for a busy machine.
        env["CI"] = "true";
        env["VOYAGE_AXE"] = axe.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        env["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        env["DOTNET_NOLOGO"] = "1";
        // A step that runs a PR's or a branch's code holds a credential only where its Variables name one (GH_TOKEN).
        var credentials = !runsCode || step?.Variables.ContainsKey("GH_TOKEN") == true;
        foreach (var e in environments) e.Lane(env, credentials);
        return env;
    }

    // A variable the default repository holds; nothing when the token may not read it.
    async Task<string?> RepositoryVariable(string name, CancellationToken ct)
    {
        try
        {
            return await Repo.Variable(name, ct);
        }
        catch (ProviderException)
        {
            return null;
        }
    }

    // A seat's environment: the lane's, without any credential a provider gave it.
    public Dictionary<string, string> SeatEnv(Dictionary<string, string> lane)
    {
        var env = new Dictionary<string, string>(lane);
        foreach (var e in environments) e.Seat(env);
        return env;
    }

    // A configured command in a checkout, with the step's arguments after its own.
    public Task<ShellResult> Run(CommandOptions command, IEnumerable<string> args, string dir, IDictionary<string, string> env, TimeSpan timeout, TextWriter log, CancellationToken ct = default)
    {
        return Commands.Run(command, args, Options, dir, CommandEnv(dir, env), timeout, log, ct, lowPriority: true);
    }

    // A command's environment: its checkout as HARNESS_WORKSPACE, and whatever names the providers'
    // own CI gives it.
    Dictionary<string, string> CommandEnv(string dir, IDictionary<string, string> env)
    {
        var here = new Dictionary<string, string>(env);
        here.TryAdd("HARNESS_WORKSPACE", dir);
        foreach (var e in environments) e.Command(here);
        return here;
    }

    // The project's preparation commands, run in a fresh checkout before a seat starts there.
    public async Task Prepare(string dir, IDictionary<string, string> env, TextWriter log)
    {
        var here = CommandEnv(dir, env);
        foreach (var command in Options.Prepare)
        {
            await Commands.Run(command, [], Options, dir, here, TimeSpan.FromMinutes(10), log);
        }
    }

    // The model a story runs on: its `{driver}:{model}` label for this agent, else the seat's own.
    public string ModelOf(IReadOnlySet<string> labels, AgentOptions seat)
    {
        return ModelOf(labels, Agent.Driver, seat.Model);
    }

    internal static string ModelOf(IReadOnlySet<string> labels, string driver, string fallback)
    {
        var prefix = driver + ":";
        var label = labels.FirstOrDefault(l => l.StartsWith(prefix, StringComparison.Ordinal));
        return label is null ? fallback : label[prefix.Length..];
    }

    // A seat's words, signed as it.
    public static string Signed(string text, AgentOptions seat)
    {
        return $"{text.TrimEnd()}\n\n{seat.Signature}";
    }
}
