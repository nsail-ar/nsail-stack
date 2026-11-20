// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSail.Harness.Agents;
using NSail.Harness.Builds;
using NSail.Harness.GitHub;
using NSail.Harness.Jobs;
using NSail.Harness.Lanes;

namespace NSail.Harness.Tests;

public class ChildEnvironmentTests
{
    [FactOn("unix")]
    public async Task AChildHoldsNoCredentialTheHarnessProcessHolds()
    {
        using var _ = Held(("GH_TOKEN", "harness-token"), ("PGPASSWORD", "db-secret"), ("ANTHROPIC_API_KEY", "key"));

        var result = await Shell.Run("env", [], Path.GetTempPath());

        Assert.Equal(0, result.ExitCode);
        var names = result.Output.Split('\n').Select(l => l.Split('=')[0]).ToHashSet();
        Assert.Contains("PATH", names);
        Assert.DoesNotContain("GH_TOKEN", names);
        Assert.DoesNotContain("PGPASSWORD", names);
        Assert.DoesNotContain("ANTHROPIC_API_KEY", names);
        Assert.DoesNotContain("harness-token", result.Output);
    }

    [FactOn("unix")]
    public async Task ANameTheCallerGivesReachesTheChild()
    {
        var result = await Shell.Run("env", [], Path.GetTempPath(), new Dictionary<string, string> { ["PGPASSWORD"] = "declared" });

        Assert.Contains("PGPASSWORD=declared", result.Output);
    }

    [FactOn("unix"), System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task ASeatRunAsAnotherUserGoesThroughSudoWithoutThisUsersHome()
    {
        var bin = Directory.CreateTempSubdirectory().FullName;
        var sudo = Path.Combine(bin, "sudo");
        Executable.Write(sudo, "#!/bin/sh\necho \"argv=$*\"\necho \"home=${HOME-unset}\"\n");
        var path = Environment.GetEnvironmentVariable("PATH");
        using var _ = Held(("PATH", $"{bin}:{path}"));
        try
        {
            var result = await Shell.Run("claude", ["-p"], Path.GetTempPath(), new Dictionary<string, string> { ["CLAUDE_CONFIG_DIR"] = "/c" }, lowPriority: true, runAs: "seat");

            Assert.Contains("argv=-n -H -u seat --preserve-env=", result.Output);
            Assert.Contains("CLAUDE_CONFIG_DIR", result.Output);
            Assert.Contains("-- setsid -w nice -n 10 claude", result.Output);
            Assert.Contains(" claude -p", result.Output);
            Assert.Contains("home=unset", result.Output);
        }
        finally
        {
            Directory.Delete(bin, recursive: true);
        }
    }

    // A Gate, main Gate or Release step runs code the harness did not write: under RunCodeAs it
    // starts as that account, a configured command and the builder's own dotnet alike.
    [FactOn("unix"), System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task AStepThatRunsCodeStartsAsTheCodeAccountWithoutThisUsersHome()
    {
        using var sudo = new FakeSudo();
        var ctx = Context("builder");

        var command = await ctx.Run(new CommandOptions { Run = "gate.cmd" }, ["main"], Path.GetTempPath(), new Dictionary<string, string>(), TimeSpan.FromMinutes(1), TextWriter.Null, runsCode: true);
        var built = await new DotNetBuilder(new BuildOptions { Solution = "S.sln" }, 1).Gate(Path.GetTempPath(), null, new Dictionary<string, string>(), TimeSpan.FromMinutes(1), TextWriter.Null, runAs: ctx.Options.RunCodeAs);

        foreach (var result in new[] { command, built })
        {
            Assert.Contains("argv=-n -H -u builder --preserve-env=", result.Output);
            Assert.Contains("home=unset", result.Output);
        }
        Assert.Contains("-- setsid -w nice -n 10 gate.cmd main", command.Output);
        Assert.Contains("-- setsid -w nice -n 10 dotnet restore S.sln", built.Output);
    }

    // K4: with no account, or for a step that runs none of a PR's code, nothing goes through sudo.
    [FactOn("unix"), System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task WithNoCodeAccountOrNoCodeAStepStartsAsTheHarnessUser()
    {
        using var sudo = new FakeSudo();

        var unset = await Context(null).Run(new CommandOptions { Run = "echo", Args = ["direct"] }, [], Path.GetTempPath(), new Dictionary<string, string>(), TimeSpan.FromMinutes(1), TextWriter.Null, runsCode: true);
        var noCode = await Context("builder").Run(new CommandOptions { Run = "echo", Args = ["direct"] }, [], Path.GetTempPath(), new Dictionary<string, string>(), TimeSpan.FromMinutes(1), TextWriter.Null);

        Assert.Equal("direct", unset.Output.Trim());
        Assert.Equal("direct", noCode.Output.Trim());
    }

    // The seats and the steps that run code build in the same slots, and a build sets the time of a file
    // only if it owns it: two accounts there fail the start, not the first gate.
    [Theory]
    [InlineData("seat", null)]
    [InlineData(null, "builder")]
    [InlineData("seat", "builder")]
    public void SeatsAndCodeUnderDifferentAccountsFailTheStart(string? seats, string? code)
    {
        var refused = Assert.Throws<InvalidOperationException>(() => Context(code, seats));

        Assert.Contains("two accounts building in one slot", refused.Message);
        Assert.Contains(seats ?? "the harness user", refused.Message);
        Assert.Contains(code ?? "unset", refused.Message);
    }

    // What the account wrote in a slot may hold a folder only it lists (a temp folder is 0700): it is removed through the account.
    [FactOn("unix"), System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task AFolderTheCodeAccountWroteInIsRemovedThroughIt()
    {
        using var sudo = new FakeSudo();
        var dir = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var refused = await Assert.ThrowsAsync<WorkspaceFault>(() => Context("builder").Remove(dir));

            Assert.Contains("argv=-n -H -u builder --preserve-env=", refused.Message);
            Assert.Contains($"-- setsid -w rm -rf {dir}", refused.Message);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task WithNoCodeAccountTheHarnessUserRemovesTheFolder()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(Path.Combine(dir, "left"), "");
        var ctx = Context(null);

        await ctx.Remove(dir);
        await ctx.Remove(dir);

        Assert.False(Directory.Exists(dir));
    }

    // What a review's or a walk's seat left read-only in its dossier goes with the reset they make through the context.
    [FactOn("unix"), System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task AReadOnlyFolderASeatLeftIsRemovedWithTheReset()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        var cache = Directory.CreateDirectory(Path.Combine(dir, "cache")).FullName;
        File.WriteAllText(Path.Combine(cache, "kept"), "");
        File.SetUnixFileMode(cache, UnixFileMode.UserRead | UnixFileMode.UserExecute);

        await Context(null).Remove(dir);

        Assert.False(Directory.Exists(dir));
    }

    static JobContext Context(string? runCodeAs)
    {
        return Context(runCodeAs, runCodeAs);
    }

    static JobContext Context(string? runCodeAs, string? seats)
    {
        var options = new HarnessOptions { RunCodeAs = runCodeAs, Repositories = [new RepositoryOptions { Name = "acme/app" }] };
        return new JobContext(null!, null!, new Dictionary<string, Workspace>(), new Seats(seats), options, [], NullLoggerFactory.Instance, new BuildLeases(1));
    }

    sealed class Seats(string? account) : IAgent
    {
        public string Driver => "test";

        public string? RunsAs => account;

        public Task<AgentResult> Run(AgentRun run, CancellationToken ct)
        {
            throw new InvalidOperationException("no seat runs here");
        }

        public IReadOnlyDictionary<string, string> ScriptEnvironment(string name)
        {
            return new Dictionary<string, string>();
        }

        public (bool Refused, DateTimeOffset? Until) Quota(string output)
        {
            return (false, null);
        }
    }

    // A sudo first on the PATH that prints the line it was given and the HOME it was started with,
    // and fails, so a builder stops at its first step.
    internal sealed class FakeSudo : IDisposable
    {
        readonly string _bin = Directory.CreateTempSubdirectory().FullName;
        readonly Held _path;

        [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
        public FakeSudo()
        {
            var sudo = Path.Combine(_bin, "sudo");
            Executable.Write(sudo, "#!/bin/sh\necho \"argv=$*\"\necho \"home=${HOME-unset}\"\nexit 1\n");
            _path = new Held([("PATH", $"{_bin}:{Environment.GetEnvironmentVariable("PATH")}")]);
        }

        public void Dispose()
        {
            _path.Dispose();
            Directory.Delete(_bin, recursive: true);
        }
    }

    [FactOn("unix")]
    public async Task OnlyTheHarnessGitCarriesItsToken()
    {
        var before = Shell.GitEnvironment;
        Shell.GitEnvironment = new Dictionary<string, string> { ["GH_TOKEN"] = "harness-token" };
        try
        {
            var other = await Shell.Run("env", [], Path.GetTempPath());
            Assert.DoesNotContain("GH_TOKEN", other.Output);
            var git = await Shell.Run("git", ["-c", "alias.tok=!echo token=$GH_TOKEN", "tok"], Path.GetTempPath());
            Assert.Contains("token=harness-token", git.Output);
        }
        finally
        {
            Shell.GitEnvironment = before;
        }
    }

    [Fact]
    public void ALaneStepThatRunsCodeHoldsTheTokenOnlyWhenItDeclaresIt()
    {
        var lane = new GitHubLaneEnvironment(new HarnessOptions { Repositories = [new RepositoryOptions { Name = "acme/app" }] }, "harness-token");

        var declared = new Dictionary<string, string>();
        lane.Lane(declared, credentials: true);
        var undeclared = new Dictionary<string, string> { ["GH_TOKEN"] = "x" };
        lane.Lane(undeclared, credentials: false);

        Assert.Equal("harness-token", declared["GH_TOKEN"]);
        Assert.DoesNotContain("GH_TOKEN", undeclared.Keys);
    }

    [Fact]
    public void ASeatHoldsNoTokenTheLaneGave()
    {
        var work = Path.Combine(Path.GetTempPath(), "seat-" + Guid.NewGuid().ToString("N"));
        var lane = new GitHubLaneEnvironment(new HarnessOptions { WorkRoot = work, Repositories = [new RepositoryOptions { Name = "acme/app" }] }, "harness-token");
        var env = new Dictionary<string, string>();
        lane.Lane(env, credentials: true);
        lane.Seat(env);

        Assert.DoesNotContain("GH_TOKEN", env.Keys);
        Assert.Equal(Path.Combine(work, "no-gh"), env["GH_CONFIG_DIR"]);
    }

    [Fact]
    public void AddingGitHubGivesTheTokenToTheHarnessGitOnly()
    {
        using var _ = Held(("GH_TOKEN", "harness-token"));
        var before = Shell.GitEnvironment;
        try
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Harness:Repositories:0:Name"] = "o/r"
            }).Build();

            new ServiceCollection().AddGitHub(configuration);

            Assert.Equal("harness-token", Shell.GitEnvironment!["GH_TOKEN"]);
        }
        finally
        {
            Shell.GitEnvironment = before;
        }
    }

    static Held Held(params (string, string)[] vars)
    {
        return new Held(vars);
    }
}

sealed class Held : IDisposable
{
    readonly (string Name, string? Old)[] old;

    public Held((string Name, string Value)[] vars)
    {
        old = vars.Select(v => (v.Name, Environment.GetEnvironmentVariable(v.Name))).ToArray();
        foreach (var (name, value) in vars) Environment.SetEnvironmentVariable(name, value);
    }

    public void Dispose()
    {
        foreach (var (name, value) in old) Environment.SetEnvironmentVariable(name, value);
    }
}
