// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Lanes;

namespace NSail.Harness.Tests;

public class WorkspaceTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "harness-ws-" + Guid.NewGuid().ToString("N"));

    string Remote => Path.Combine(_root, "remote.git");

    string Bare => Path.Combine(_root, "ws", "repo.git");

    public void Dispose()
    {
        if (!Directory.Exists(_root)) return;
        foreach (var f in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
        Directory.Delete(_root, true);
    }

    async Task<Workspace> Warm()
    {
        Directory.CreateDirectory(_root);
        await Shell.Git(_root, "init", "-q", "--bare", "-b", "main", Remote);
        var seed = Path.Combine(_root, "seed");
        await Shell.Git(_root, "clone", "-q", Remote, seed);
        await Shell.Git(seed, "checkout", "-q", "-b", "main");
        await Shell.Git(seed, "-c", "user.name=t", "-c", "user.email=t@example.com", "commit", "-q", "--allow-empty", "-m", "M");
        await Shell.Git(seed, "push", "-q", "origin", "main");
        var ws = new Workspace(Path.Combine(_root, "ws"), Remote, null, 1, "Gru", "gru@example.com");
        await ws.Prepare(1);
        return ws;
    }

    static string Script(string marker)
    {
        return $"#!/bin/sh\necho ran > '{marker}'\n";
    }

    [Fact]
    public async Task APlantedHookAndFsmonitorNeverRunInTheHarnessGit()
    {
        if (OperatingSystem.IsWindows()) return;
        var ws = await Warm();
        var hook = Path.Combine(_root, "hook-ran");
        var monitor = Path.Combine(_root, "monitor-ran");
        var hooks = Path.Combine(Bare, "hooks");
        Directory.CreateDirectory(hooks);
        foreach (var name in new[] { "post-checkout", "post-merge", "reference-transaction" })
        {
            var path = Path.Combine(hooks, name);
            File.WriteAllText(path, Script(hook));
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        var fsmonitor = Path.Combine(_root, "fsmonitor.sh");
        File.WriteAllText(fsmonitor, Script(monitor));
        File.SetUnixFileMode(fsmonitor, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        File.AppendAllText(Path.Combine(Bare, "config"), $"[core]\n\tfsmonitor = {fsmonitor}\n\thooksPath = {hooks}\n[alias]\n\tcheckout = !touch {hook}\n");

        var slot = ws.SlotPath(1);
        await Shell.Git(slot, "checkout", "-q", "--detach", "-f", ws.Tip);
        await Shell.Git(slot, "status", "-s");
        await ws.Prepare(1);

        Assert.False(File.Exists(hook));
        Assert.False(File.Exists(monitor));
    }

    [Fact]
    public async Task ASeatsConfigIsGoneOnceTheHarnessRestoresIt()
    {
        var ws = await Warm();
        var before = File.ReadAllText(Path.Combine(Bare, "config"));
        await Shell.Git(Bare, "config", "core.fsmonitor", "evil");
        await Shell.Git(Bare, "config", "alias.push", "!evil");
        await Shell.Git(Bare, "config", "--add", "credential.helper", "evil");
        await Shell.Git(Bare, "config", "user.name", "someone else");
        await Shell.Git(Bare, "config", "core.symlinks", "false");
        await Shell.Git(Bare, "config", "core.ignorecase", "true");
        await Shell.Git(Bare, "config", "remote.origin.url", "ext::sh -c evil");
        Directory.CreateDirectory(Path.Combine(Bare, "hooks"));
        File.WriteAllText(Path.Combine(Bare, "hooks", "pre-push"), "x");

        await ws.Restore();

        Assert.Equal(before, File.ReadAllText(Path.Combine(Bare, "config")));
        Assert.False(File.Exists(Path.Combine(Bare, "hooks", "pre-push")));
    }

    [Fact]
    public async Task AWarmSlotKeepsWhatGitIgnores()
    {
        var ws = await Warm();
        var obj = Path.Combine(ws.SlotPath(1), "obj");
        Directory.CreateDirectory(obj);
        File.WriteAllText(Path.Combine(obj, "a"), "x");
        File.AppendAllText(Path.Combine(Bare, "info", "exclude"), "/obj/\n");

        await ws.Prepare(1);

        Assert.True(File.Exists(Path.Combine(obj, "a")));
    }

    [Fact]
    public async Task PrepareKeepsALocalBranchAheadOfItsRemoteAndDropsOneThatIsNot()
    {
        var ws = await Warm();
        await Shell.Git(Bare, "branch", "pushed", ws.Tip);
        await Shell.Git(Bare, "branch", "ahead", ws.Tip);
        var slot = ws.SlotPath(1);
        await Shell.Git(slot, "checkout", "-q", "-B", "ahead", ws.Tip);
        await Shell.Git(slot, "-c", "user.name=t", "-c", "user.email=t@example.com", "commit", "-q", "--allow-empty", "-m", "unpushed");

        await ws.Prepare(1);

        var heads = await Shell.Git(Bare, "for-each-ref", "--format=%(refname:short)", "refs/heads");
        Assert.Contains("ahead", heads);
        Assert.DoesNotContain("pushed", heads);
    }
}
