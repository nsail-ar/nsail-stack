// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Agents;
using NSail.Harness.Claude;

namespace NSail.Harness.Tests;

// A seat without its doctrine never starts.
public class ClaudePluginTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "harness-claude-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    // A stand-in `claude` that records each call and fails the ones whose arguments contain `failing`.
    ClaudeAgent Agent(string failing, out string calls)
    {
        Directory.CreateDirectory(_root);
        calls = Path.Combine(_root, "calls.txt");
        var script = Path.Combine(_root, "claude");
        File.WriteAllText(script, $"#!/bin/sh\necho \"$*\" >> '{calls}'\ncase \"$*\" in *{failing}*) exit 3;; -p*) cat >/dev/null;; esac\nexit 0\n");
        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return new ClaudeAgent(new ClaudeOptions { Command = script, Plugins = ["nsail-stack@market", "nsail-crew@market"] }, Path.Combine(_root, "config"));
    }

    AgentRun Run()
    {
        var dir = Path.Combine(_root, "slot");
        Directory.CreateDirectory(dir);
        return new AgentRun("go", dir, "m", AgentAccess.Builder, 1, TimeSpan.FromMinutes(1), new Dictionary<string, string>(), Path.Combine(_root, "log.txt"), Marketplace: "/doctrine");
    }

    [Fact]
    public async Task AFailedPluginInstallStopsTheJobBeforeTheAgentAndNamesThePlugin()
    {
        if (OperatingSystem.IsWindows()) return;
        var agent = Agent("nsail-crew", out var calls);

        var result = await agent.Run(Run(), CancellationToken.None);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("nsail-crew@market", result.Result);
        var lines = File.ReadAllLines(calls);
        Assert.DoesNotContain(lines, l => l.StartsWith("-p"));
        Assert.Equal(3, lines.Length);
    }

    [Fact]
    public async Task AFailedMarketplaceAddStopsTheJobBeforeAnyInstall()
    {
        if (OperatingSystem.IsWindows()) return;
        var agent = Agent("marketplace", out var calls);

        var result = await agent.Run(Run(), CancellationToken.None);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("/doctrine", result.Result);
        Assert.Single(File.ReadAllLines(calls));
    }

    [Fact]
    public async Task WhenEveryInstallSucceedsTheAgentStarts()
    {
        if (OperatingSystem.IsWindows()) return;
        var agent = Agent("never-matches", out var calls);

        var result = await agent.Run(Run(), CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(File.ReadAllLines(calls), l => l.StartsWith("-p"));
    }
}
