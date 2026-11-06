// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text;
using System.Text.Json;
using NSail.Harness.Api;
using NSail.Harness.Hosting;
using NSail.Harness.Jobs;
using NSail.Harness.Lanes;
using NSail.Harness.Planning;

namespace NSail.Harness.Tests;

// A running job's line and its silence: an agent's last words off its stream, a build's step, when
// its log last moved and the limit past which it reads as stale.
public class JobActivityTests : IDisposable
{
    const string Token = "ghp_TOKENshapedVALUE0123456789abcdefABCDEF";
    static readonly TimeSpan Ceiling = TimeSpan.FromMinutes(30);
    static readonly HarnessOptions Options = new() { Repositories = [new RepositoryOptions { Name = "acme/app" }] };
    readonly string _root = Path.Combine(Path.GetTempPath(), "job-activity-" + Guid.NewGuid().ToString("N"));

    public JobActivityTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    static string Said(params object[] content)
    {
        return JsonSerializer.Serialize(new { type = "assistant", message = new { role = "assistant", content } });
    }

    static string Text(string text)
    {
        return Said(new { type = "text", text });
    }

    static string ToolUse(string command)
    {
        return Said(new { type = "tool_use", id = "t1", name = "Bash", input = new { command } });
    }

    static string ToolResult(string output)
    {
        return JsonSerializer.Serialize(new { type = "user", message = new { role = "user", content = new object[] { new { type = "tool_result", tool_use_id = "t1", content = output } } } });
    }

    string Log(params string[] lines)
    {
        var path = Path.Combine(_root, Guid.NewGuid().ToString("N") + ".log");
        File.WriteAllText(path, string.Join('\n', lines) + "\n");
        return path;
    }

    static JobStep Step(int minutes, bool command = false)
    {
        return new JobStep(new WorkerOptions(), command ? null : new AgentOptions(), command ? new CommandOptions { Run = "walk.sh" } : null, minutes, new Dictionary<string, string>());
    }

    static RunningJob Job(WorkKind kind, string log)
    {
        return new RunningJob("job-1", new WorkItem(kind, 42, kind is WorkKind.Gate or WorkKind.Review ? 7 : null, "work", "test", Worker: "w"), 2, DateTimeOffset.UtcNow.AddHours(-3), log);
    }

    [Fact]
    public void AnAgentsLineIsItsLastTextsFirstLineNeverATool()
    {
        var log = Log(
            "== dev #42 on box slot 2",
            Text("Reading the story."),
            Text("Both E2E walks pass. Now the docs: guide, ops.md, then preflight.\nThen the commit."),
            ToolUse($"echo {Token} && dotnet test"),
            ToolResult($"GITHUB_TOKEN={Token}\nPassed!"),
            "{\"type\":\"user\",\"message\":{broken json");

        var job = JobActivity.Read(Job(WorkKind.Dev, log), Step(240), Ceiling, waitingTurn: false);

        Assert.Equal("Both E2E walks pass. Now the docs: guide, ops.md, then preflight.…", job.Doing);
    }

    [Fact]
    public void AQaScriptsAgentSaysItsLineUnderItsStepsMinutes()
    {
        var log = Log("== qa #42 on box slot 2", Text("Walking the invoice screen."), ToolUse("dotnet test"));

        var script = JobActivity.Read(Job(WorkKind.Qa, log), Step(180, command: true), Ceiling, waitingTurn: false);
        var seat = JobActivity.Read(Job(WorkKind.Qa, log), Step(180), Ceiling, waitingTurn: false);
        var devWithCommand = JobActivity.Read(Job(WorkKind.Dev, log), Step(240, command: true), Ceiling, waitingTurn: false);

        Assert.Equal("Walking the invoice screen.", script.Doing);
        Assert.Equal(TimeSpan.FromMinutes(180), script.SilenceLimit);
        Assert.Equal(Ceiling, seat.SilenceLimit);
        Assert.Equal("Walking the invoice screen.", devWithCommand.Doing);
        Assert.Equal(Ceiling, devWithCommand.SilenceLimit);
    }

    [Fact]
    public void ALongLineIsCutAtTheCapWithAnEllipsis()
    {
        var log = Log(Text(new string('a', 150) + " " + new string('b', 150)));

        var doing = JobActivity.Read(Job(WorkKind.Analyze, log), Step(45), Ceiling, waitingTurn: false).Doing!;

        Assert.Equal(JobActivity.LineCap, doing.Length);
        Assert.EndsWith("…", doing);
        Assert.Equal("one line", JobActivity.Line("\n  one line  \n"));
    }

    [Fact]
    public void AGatesLineIsItsStepWaitingThenBuildingThenTesting()
    {
        var leases = new BuildLeases(1);
        var gate = Job(WorkKind.Gate, Log("== gate PR #7 at abcdef12 on box slot 2"));
        var holder = JobContext.HolderOf(gate.Item);
        var other = leases.Acquire("dev-9", 0, default).Result;
        var waiting = leases.Acquire(holder, 5, default);

        var inLine = JobActivity.Read(gate, Step(60, command: true), Ceiling, leases.Waits(holder));
        leases.Release(other.Id);
        Assert.True(waiting.Wait(TimeSpan.FromSeconds(2)));
        File.AppendAllText(gate.LogPath, "== build turn taken\n> dotnet restore NSail.sln\n> dotnet build affected.slnf\n");
        var building = JobActivity.Read(gate, Step(60, command: true), Ceiling, leases.Waits(holder));
        File.AppendAllText(gate.LogPath, "> dotnet test affected-tests.slnf --no-build\n  Passed Some.Test [12 ms]\n");
        var testing = JobActivity.Read(gate, Step(60, command: true), Ceiling, leases.Waits(holder));

        Assert.Equal(RunningJob.WaitingForTurn, inLine.Doing);
        Assert.Equal(RunningJob.Building, building.Doing);
        Assert.Equal(RunningJob.Testing, testing.Doing);
        Assert.Equal(RunningJob.Testing, JobActivity.BuildStep(["Build succeeded.", "Test run for /w/bin/App.Tests.dll (.NETCoreApp,Version=v10.0)"]));
    }

    [Fact]
    public void AJobIsStalePastTheLimitItAlreadyRunsUnder()
    {
        var log = Log(Text("Working."));
        var quiet = DateTimeOffset.UtcNow.AddMinutes(-31);
        File.SetLastWriteTimeUtc(log, quiet.UtcDateTime);
        var now = DateTimeOffset.UtcNow;

        var agent = JobActivity.Read(Job(WorkKind.Dev, log), Step(240), Ceiling, waitingTurn: false);
        var agentAt29 = agent with { LastWrote = now.AddMinutes(-29) };
        var gate = JobActivity.Read(Job(WorkKind.Gate, log), Step(60, command: true), Ceiling, waitingTurn: false);
        var main = JobActivity.Read(Job(WorkKind.Main, log), Step(20, command: true), Ceiling, waitingTurn: false);
        var inLine = JobActivity.Read(Job(WorkKind.Gate, log), Step(10, command: true), Ceiling, waitingTurn: true);
        var release = JobActivity.Read(Job(WorkKind.Release, log), Step(150, command: true), Ceiling, waitingTurn: false);

        Assert.Equal(quiet.ToUnixTimeSeconds(), agent.LastWrote!.Value.ToUnixTimeSeconds());
        Assert.Equal(Ceiling, agent.SilenceLimit);
        Assert.True(agent.StaleAt(now));
        Assert.False(agentAt29.StaleAt(now));
        Assert.Equal(TimeSpan.FromMinutes(60), gate.SilenceLimit);
        Assert.False(gate.StaleAt(now));
        Assert.Equal(TimeSpan.FromMinutes(40), main.SilenceLimit);
        Assert.Null(inLine.SilenceLimit);
        Assert.False(inLine.StaleAt(now.AddDays(1)));
        Assert.Equal(TimeSpan.FromMinutes(150), release.SilenceLimit);
        Assert.Null(release.Doing);
    }

    [Fact]
    public void ALogThatCannotBeReadLeavesADash()
    {
        var missing = JobActivity.Read(Job(WorkKind.Dev, Path.Combine(_root, "gone.log")), Step(240), Ceiling, waitingTurn: false);
        var held = Log(Text("Still writing."));
        using var writer = new StreamWriter(held, append: true) { AutoFlush = true };
        writer.WriteLine(Text("Writing now."));
        var open = JobActivity.Read(Job(WorkKind.Dev, held), Step(240), Ceiling, waitingTurn: false);
        var html = new StringBuilder();
        StatusPage.Running(html, Options, [missing], DateTimeOffset.UtcNow);

        Assert.Null(missing.Doing);
        Assert.Null(missing.LastWrote);
        Assert.False(missing.StaleAt(DateTimeOffset.UtcNow));
        Assert.Contains("slot 2 · —", html.ToString());
        Assert.Equal("Writing now.", open.Doing);
        Assert.NotNull(open.LastWrote);
    }

    [Fact]
    public void NoTokenOfAToolOrTheEnvironmentReachesTheReport()
    {
        var log = Log(
            Text("Running the tests."),
            ToolUse($"GITHUB_TOKEN={Token} gh pr list"),
            ToolResult($"{Token}\nGITHUB_TOKEN={Token}"));
        var job = JobActivity.Read(Job(WorkKind.Review, log), Step(60), Ceiling, waitingTurn: false);
        var status = new HarnessStatus { Repository = "acme/app", Machine = "box", Running = [job] };

        var json = JsonSerializer.Serialize(new HarnessReport { Machine = "box", Status = status }, JsonSerializerOptions.Web);

        Assert.Contains("Running the tests.", json);
        Assert.DoesNotContain(Token, json);
        Assert.Contains("\"lastWrote\"", json);
        Assert.Contains("\"silenceLimit\"", json);
    }

    [Fact]
    public void TheRunningRowCarriesTheLineAndTheAgeAndRecentJobsDoNot()
    {
        var now = DateTimeOffset.UtcNow;
        var fresh = Job(WorkKind.Dev, "x.log") with { Doing = "Both E2E walks pass.", LastWrote = now.AddMinutes(-3), SilenceLimit = Ceiling };
        var stale = Job(WorkKind.Gate, "y.log") with { Id = "job-2", Doing = RunningJob.Testing, LastWrote = now.AddMinutes(-70), SilenceLimit = TimeSpan.FromMinutes(60) };
        var running = new StringBuilder();
        StatusPage.Running(running, Options, [fresh, stale], now);
        var recent = new StringBuilder();
        JobsPage.Table(recent, Options, [JobsPage.RowOf(new JobRecord("job-3", "dev #42", 42, now.AddHours(-1), now, "pushed", "z.log", "ok"))], now);

        var html = running.ToString();
        Assert.Contains("slot 2 · Both E2E walks pass.", html);
        Assert.Contains("its last words, not a verified state", html);
        Assert.Contains("wrote 3 m ago", html);
        Assert.Contains("slot 2 · testing", html);
        Assert.Contains("wrote 1 h 10 m ago", html);
        Assert.Contains(">stale<", html);
        Assert.DoesNotContain("wrote ", recent.ToString());
        Assert.DoesNotContain("doing", recent.ToString());
    }
}
