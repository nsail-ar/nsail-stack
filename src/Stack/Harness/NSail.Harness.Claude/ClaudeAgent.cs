// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Agents;
using NSail.Harness.Lanes;

namespace NSail.Harness.Claude;

// `claude -p` with the prompt on stdin and stream-json out. Each lane slot has its own
// CLAUDE_CONFIG_DIR, emptied of plugins before the run: a plugin an earlier job installed at
// another commit would shadow the doctrine this job reads.
public sealed class ClaudeAgent(ClaudeOptions options, string configRoot) : IAgent
{
    // The account's quota windows from the newest run that reported them: the plans read them.
    volatile IReadOnlyList<UsageWindow> _usage = [];

    public IReadOnlyList<UsageWindow> Usage => _usage;

    void Heard(StreamSignals signals)
    {
        if (signals.Usage.Count > 0) _usage = signals.Usage;
    }

    public string Driver => "claude";

    static readonly TimeSpan Foreground = TimeSpan.FromMinutes(30);

    public TimeSpan? LongestCommand => Foreground;

    public async Task<AgentResult> Run(AgentRun run, CancellationToken ct)
    {
        var command = options.Resolve();
        var config = Path.Combine(configRoot, Path.GetFileName(run.WorkDir));
        Directory.CreateDirectory(config);
        var plugins = Path.Combine(config, "plugins");
        if (Directory.Exists(plugins))
        {
            if (options.RunAs is null)
            {
                Directory.Delete(plugins, recursive: true);
            }
            else
            {
                // The account created it, so the account removes it.
                var removed = await Shell.Run("rm", ["-rf", plugins], config, ct: ct, runAs: options.RunAs);
                if (removed.ExitCode != 0)
                {
                    throw new InvalidOperationException($"{plugins} could not be emptied as {options.RunAs}: {removed.Output.Trim()}");
                }
            }
        }

        var env = new Dictionary<string, string>(run.Env)
        {
            ["CLAUDE_CONFIG_DIR"] = config,
            // Headless, a turn that ends is the job ending: nothing may go to the background,
            // and a long command runs in the foreground up to Foreground.
            ["CLAUDE_CODE_DISABLE_BACKGROUND_TASKS"] = "1",
            ["BASH_DEFAULT_TIMEOUT_MS"] = "900000",
            ["BASH_MAX_TIMEOUT_MS"] = ((long)Foreground.TotalMilliseconds).ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        if (options.Token() is { } token) env["CLAUDE_CODE_OAUTH_TOKEN"] = token;
        if (GitBash() is { } gitBash) env["CLAUDE_CODE_GIT_BASH_PATH"] = gitBash;

        Directory.CreateDirectory(Path.GetDirectoryName(run.LogPath)!);
        await using var log = new StreamWriter(run.LogPath, append: true) { AutoFlush = true };

        if (run.Marketplace is not null && options.Plugins.Count > 0)
        {
            var added = await Shell.Run(command, ["plugin", "marketplace", "add", run.Marketplace], run.WorkDir, env, TimeSpan.FromMinutes(5), log, ct: ct, runAs: options.RunAs);
            if (Failure(added) is { } addFailure) throw await Unequipped(log, $"marketplace {run.Marketplace} was not added", addFailure);
            foreach (var plugin in options.Plugins)
            {
                var installed = await Shell.Run(command, ["plugin", "install", plugin], run.WorkDir, env, TimeSpan.FromMinutes(5), log, ct: ct, runAs: options.RunAs);
                if (Failure(installed) is { } failure) throw await Unequipped(log, $"plugin {plugin} did not install", failure);
            }
        }

        string[] args =
        [
            "-p", "--output-format", "stream-json", "--verbose",
            "--model", options.ModelOf(run.Model),
            "--max-turns", run.MaxTurns.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--tools", Loaded(run.Access, run.Tools),
            "--allowedTools", string.Join(',', [Allowed(run.Access), .. run.Tools ?? []]),
            "--disallowedTools", Disallowed(run.Access),
            .. run.Mcp is null ? Array.Empty<string>() : ["--mcp-config", run.Mcp, "--strict-mcp-config"],
            .. options.Settings() is { } settings ? ["--settings", settings] : Array.Empty<string>(),
        ];
        var result = await Shell.Run(command, args, run.WorkDir, env, run.Timeout, log, stdin: run.Prompt, ct: ct, lowPriority: true, runAs: options.RunAs, keep: StreamSignals.IsSignal);
        var signals = StreamSignals.Read(result.Output);
        Heard(signals);
        return new AgentResult(result.ExitCode, result.TimedOut, signals.QuotaUntil, signals.QuotaRefused, signals.Result);
    }

    static string? Failure(ShellResult r)
    {
        return r.TimedOut ? "timed out" : r.ExitCode != 0 ? $"exit {r.ExitCode}" : null;
    }

    // A seat without its doctrine must not start. It is the machine's break, not the story's: the
    // harness says it on the story as a broken job, naming what failed.
    static async Task<InvalidOperationException> Unequipped(StreamWriter log, string failed, string why)
    {
        var said = $"{failed} ({why}); the agent was not started";
        await log.WriteLineAsync(said);
        return new InvalidOperationException(said);
    }

    // A seat's access in Claude Code's tools. A reader writes only through Write (its out files) and
    // runs only git's reading commands; nobody gets gh or a push, and nobody gets the tools that end
    // the turn under a subagent or a timer (SendMessage, Agent, Task, Monitor, ...).
    static string Allowed(AgentAccess access)
    {
        return access switch
        {
            AgentAccess.Builder => "Bash,Read,Edit,MultiEdit,Write,Glob,Grep,TodoWrite,Skill",
            AgentAccess.Walker => "Read,Glob,Grep,Write,Skill",
            _ => "Read,Glob,Grep,Write,Skill,Bash(git log:*),Bash(git show:*),Bash(git grep:*),Bash(git diff:*),Bash(git merge-base:*)",
        };
    }

    // The built-in tools a seat's run loads at all: the ones its access and its worker's tools name.
    // Every other one would only be context the seat pays for on each turn and never calls; an MCP
    // server's tools are not built in, and come with the server.
    internal static string Loaded(AgentAccess access, IReadOnlyList<string>? tools)
    {
        var names = Allowed(access).Split(',').Concat(tools ?? [])
            .Select(t => t.Split('(')[0].Trim())
            .Where(t => t.Length > 0 && !t.StartsWith("mcp__", StringComparison.Ordinal));
        return string.Join(',', names.Distinct(StringComparer.Ordinal));
    }

    static string Disallowed(AgentAccess access)
    {
        return access switch
        {
            AgentAccess.Builder => "SendMessage,Agent,Task,Monitor,ScheduleWakeup,TaskOutput,CronCreate,Bash(gh:*),Bash(git push:*)",
            AgentAccess.Walker => "Edit,MultiEdit,SendMessage,Agent,Task,Monitor,WebFetch,WebSearch,Bash(gh:*),Bash(git push:*)",
            _ => "Edit,MultiEdit,SendMessage,Agent,Task,Bash(gh:*)",
        };
    }

    public IReadOnlyDictionary<string, string> ScriptEnvironment(string name)
    {
        var config = Path.Combine(configRoot, name);
        Directory.CreateDirectory(config);
        var env = new Dictionary<string, string>
        {
            ["CLAUDE_BIN"] = options.Resolve(),
            ["CLAUDE_CONFIG_DIR"] = config,
            ["CLAUDE_CODE_DISABLE_BACKGROUND_TASKS"] = "1",
        };
        if (options.Token() is { } token) env["CLAUDE_CODE_OAUTH_TOKEN"] = token;
        return env;
    }

    public bool IsSignal(string line)
    {
        return StreamSignals.IsSignal(line);
    }

    public (bool Refused, DateTimeOffset? Until) Quota(string output)
    {
        var signals = StreamSignals.Read(output);
        Heard(signals);
        return (signals.QuotaRefused, signals.QuotaUntil);
    }

    static string? GitBash()
    {
        if (!OperatingSystem.IsWindows()) return null;
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("CLAUDE_CODE_GIT_BASH_PATH"),
            @"C:\Program Files\Git\bin\bash.exe",
        };
        return candidates.FirstOrDefault(c => !string.IsNullOrEmpty(c) && File.Exists(c));
    }
}
