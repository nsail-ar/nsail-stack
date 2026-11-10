// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.GitHub;
using NSail.Harness.Lanes;

namespace NSail.Harness.Tests;

// A real git remote in a temp dir: a file:// remote never asks the credential helper for the token.
public class PushTests : IDisposable
{
    const string Branch = "story/oz-1";
    readonly string _root = Path.Combine(Path.GetTempPath(), "harness-push-" + Guid.NewGuid().ToString("N"));
    readonly GitHubRepository _repo = new(null!, new RepositoryOptions { Name = "acme/app" }, new StoryNames("oz", ["story"]), "t");

    string Remote => Path.Combine(_root, "remote.git");

    public PushTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (!Directory.Exists(_root)) return;
        foreach (var f in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
        Directory.Delete(_root, true);
    }

    async Task<string> Clone(string name)
    {
        var dir = Path.Combine(_root, name);
        await Shell.Git(_root, "clone", "-q", Remote, dir);
        await Shell.Git(dir, "config", "user.name", "t");
        await Shell.Git(dir, "config", "user.email", "t@example.com");
        await Shell.Git(dir, "config", "commit.gpgsign", "false");
        return dir;
    }

    static async Task<string> Commit(string dir, string message)
    {
        await Shell.Git(dir, "commit", "-q", "--allow-empty", "-m", message);
        return await Shell.Git(dir, "rev-parse", "HEAD");
    }

    async Task<string> RemoteHead(string branch)
    {
        return await Shell.Git(Remote, "rev-parse", $"refs/heads/{branch}");
    }

    // main at M, the story branch at A on top of it; a clone checked out on the branch, as Checkout leaves it.
    async Task<(string Dir, string Main, string Start)> Sailing()
    {
        await Shell.Git(_root, "init", "-q", "--bare", Remote);
        var seed = await Clone("seed");
        await Shell.Git(seed, "checkout", "-q", "-b", "main");
        var main = await Commit(seed, "M");
        await Shell.Git(seed, "push", "-q", "origin", "main");
        await Shell.Git(seed, "checkout", "-q", "-b", Branch);
        var start = await Commit(seed, "A");
        await Shell.Git(seed, "push", "-q", "origin", Branch);
        var dir = await Clone("slot");
        await Shell.Git(dir, "checkout", "-q", "-B", Branch, start);
        return (dir, main, start);
    }

    [Fact]
    public async Task ARewrittenBranchOverAnUnmovedRemoteReplacesItsHead()
    {
        var (dir, main, start) = await Sailing();
        await Shell.Git(dir, "reset", "-q", "--hard", main);
        var rewritten = await Commit(dir, "B'");

        await _repo.Push(dir, Branch, start);

        Assert.Equal(rewritten, await RemoteHead(Branch));
    }

    [Fact]
    public async Task ARemoteThatMovedUnderTheVoyageIsNeverOverwritten()
    {
        var (dir, main, start) = await Sailing();
        var other = await Clone("other");
        await Shell.Git(other, "checkout", "-q", Branch);
        var theirs = await Commit(other, "C");
        await Shell.Git(other, "push", "-q", "origin", Branch);
        // The shared clone's remote-tracking ref is refetched by another slot: the lease must not read it.
        await Shell.Git(dir, "fetch", "-q", "origin");
        await Shell.Git(dir, "reset", "-q", "--hard", main);
        await Commit(dir, "B'");

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => _repo.Push(dir, Branch, start));

        Assert.Contains($"push to {Branch} refused", refused.Message);
        Assert.Contains("rejected", refused.Message);
        Assert.Equal(theirs, await RemoteHead(Branch));
    }

    [Fact]
    public async Task CommitsOnTopOfTheRemoteHeadGoUp()
    {
        var (dir, _, start) = await Sailing();
        var added = await Commit(dir, "B");

        await _repo.Push(dir, Branch, start);

        Assert.Equal(added, await RemoteHead(Branch));
    }

    [Fact]
    public async Task ANewBranchGoesUp()
    {
        var (dir, main, _) = await Sailing();
        await Shell.Git(dir, "checkout", "-q", "-B", "story/oz-2", main);
        var added = await Commit(dir, "N");

        await _repo.Push(dir, "story/oz-2", null);

        Assert.Equal(added, await RemoteHead("story/oz-2"));
    }

    [Fact]
    public async Task ANewBranchSomeoneElseOpenedMeanwhileIsNeverOverwritten()
    {
        var (dir, _, start) = await Sailing();
        await Commit(dir, "B");

        await Assert.ThrowsAsync<InvalidOperationException>(() => _repo.Push(dir, Branch, null));

        Assert.Equal(start, await RemoteHead(Branch));
    }

    [Fact]
    public async Task RescuePutsCommitsAheadOnARescueRefAndNeverOverwritesOne()
    {
        var (dir, _, start) = await Sailing();
        var added = await Commit(dir, "B");

        var first = await DevJob.RescueRef(_repo, dir, start, "rescue/oz-1", []);
        var later = await Commit(dir, "C");
        var second = await DevJob.RescueRef(_repo, dir, start, "rescue/oz-1", []);

        Assert.Equal("rescue/oz-1", first);
        Assert.Equal("rescue/oz-1-2", second);
        Assert.Equal(added, await RemoteHead("rescue/oz-1"));
        Assert.Equal(later, await RemoteHead("rescue/oz-1-2"));
        Assert.Equal(start, await RemoteHead(Branch));
    }

    [Fact]
    public async Task RescueWithNothingAheadPushesNothing()
    {
        var (dir, _, start) = await Sailing();

        Assert.Null(await DevJob.RescueRef(_repo, dir, start, "rescue/oz-1", []));
    }

    [Fact]
    public async Task RescueWithAProtectedPathStaysLocalUnlessTheScreenIsLifted()
    {
        var (dir, _, start) = await Sailing();
        Directory.CreateDirectory(Path.Combine(dir, ".github", "workflows"));
        await File.WriteAllTextAsync(Path.Combine(dir, ".github", "workflows", "ci.yml"), "on: push");
        await Shell.Git(dir, "add", "-A");
        await Commit(dir, "W");

        Assert.Null(await DevJob.RescueRef(_repo, dir, start, "rescue/oz-1", [".github/workflows/**"]));
        Assert.Equal("rescue/oz-1", await DevJob.RescueRef(_repo, dir, start, "rescue/oz-1", []));
    }

    [Fact]
    public async Task RescueRefusedForAnyReasonButAnExistingRefTriesOnce()
    {
        var (dir, _, start) = await Sailing();
        await Commit(dir, "B");
        var tally = Path.Combine(_root, "tally");
        var hook = Path.Combine(Remote, "hooks", "pre-receive");
        await File.WriteAllTextAsync(hook, $"#!/bin/sh\necho x >> '{tally.Replace('\\', '/')}'\nexit 1\n");
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(hook, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        await Assert.ThrowsAsync<InvalidOperationException>(() => DevJob.RescueRef(_repo, dir, start, "rescue/oz-1", []));

        Assert.Single(await File.ReadAllLinesAsync(tally));
    }
}
