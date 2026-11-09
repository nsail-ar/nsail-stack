// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSail.Harness.GitHub;
using NSail.Harness.Lanes;

namespace NSail.Harness.Tests;

public class ChildEnvironmentTests
{
    [Fact]
    public async Task AChildHoldsNoCredentialTheHarnessProcessHolds()
    {
        if (OperatingSystem.IsWindows()) return;
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

    [Fact]
    public async Task ANameTheCallerGivesReachesTheChild()
    {
        if (OperatingSystem.IsWindows()) return;

        var result = await Shell.Run("env", [], Path.GetTempPath(), new Dictionary<string, string> { ["PGPASSWORD"] = "declared" });

        Assert.Contains("PGPASSWORD=declared", result.Output);
    }

    [Fact]
    public async Task ASeatRunAsAnotherUserGoesThroughSudoWithoutThisUsersHome()
    {
        if (OperatingSystem.IsWindows()) return;
        var bin = Directory.CreateTempSubdirectory().FullName;
        var sudo = Path.Combine(bin, "sudo");
        File.WriteAllText(sudo, "#!/bin/sh\necho \"argv=$*\"\necho \"home=${HOME-unset}\"\n");
        File.SetUnixFileMode(sudo, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var path = Environment.GetEnvironmentVariable("PATH");
        using var _ = Held(("PATH", $"{bin}:{path}"));
        try
        {
            var result = await Shell.Run("claude", ["-p"], Path.GetTempPath(), new Dictionary<string, string> { ["CLAUDE_CONFIG_DIR"] = "/c" }, lowPriority: true, runAs: "seat");

            Assert.Contains("argv=-n -H -u seat --preserve-env=", result.Output);
            Assert.Contains("CLAUDE_CONFIG_DIR", result.Output);
            Assert.Contains("-- setsid -w sh -c", result.Output);
            Assert.Contains(" nice -n 10 claude -p", result.Output);
                        Assert.Contains("home=unset", result.Output);
        }
        finally
        {
            Directory.Delete(bin, recursive: true);
        }
    }

    [Fact]
    public async Task OnlyTheHarnessGitCarriesItsToken()
    {
        if (OperatingSystem.IsWindows()) return;
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
