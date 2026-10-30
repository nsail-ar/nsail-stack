// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Problems;
using NSail.Storage.Git;
using NSail.Storage.Providers;
using System.Diagnostics;
using System.Text;

namespace NSail.Storage.Tests;

// The provider against a bare repository on the local disk — the remote git speaks to with no
// server — so what is proven is the real executable committing and pushing, not a fake of it.
public sealed class GitStorageTests : IDisposable
{
    readonly Container _remote = new();
    readonly GitStorageProvider _provider = new();

    public GitStorageTests()
    {
        Git(_remote.Path, "init", "--bare", "--quiet", "--initial-branch=main", "store.git");
    }

    StorageTarget Target
    {
        get
        {
            return new StorageTarget("git", _remote.Path, "", "store.git", "", "", "", "https://raw.example.test/org/store/main/{key}");
        }
    }

    [Fact]
    public async Task PutsAndGetsAnObjectBackByteForByte()
    {
        var payload = Encoding.UTF8.GetBytes("a report the provider commits");

        await Put("reports/2026/ledger.csv", payload);

        using var stored = await _provider.Get(Target, "reports/2026/ledger.csv");

        Assert.NotNull(stored);

        using var readBack = new MemoryStream();
        await stored.Content.CopyToAsync(readBack);

        Assert.Equal(payload, readBack.ToArray());
        Assert.Equal(payload.Length, stored.Length);
    }

    [Fact]
    public async Task EveryPutIsACommitOnTheBranch()
    {
        await Put("a.txt", "one"u8.ToArray());
        await Put("a.txt", "two"u8.ToArray());
        await Put("a.txt", "two"u8.ToArray());

        Assert.Equal("2", Git(Path.Combine(_remote.Path, "store.git"), "rev-list", "--count", "main").Trim());
    }

    [Fact]
    public async Task AnswersNullForAnObjectThatIsNotThere()
    {
        Assert.Null(await _provider.Get(Target, "never.txt"));

        await Put("a.txt", "one"u8.ToArray());

        Assert.Null(await _provider.Get(Target, "never.txt"));
    }

    [Fact]
    public async Task ListsWhatWasPutUnderAPrefix()
    {
        await Put("reports/a.csv", "12345"u8.ToArray());
        await Put("reports/b.csv", "1"u8.ToArray());
        await Put("backups/c.zip", "1"u8.ToArray());

        var listed = await _provider.List(Target, "reports/");

        Assert.Equal(["reports/a.csv", "reports/b.csv"], listed.Select(stored => stored.Key));
        Assert.Equal(5, listed[0].Length);
        Assert.Equal(3, (await _provider.List(Target, null)).Count);
    }

    [Fact]
    public async Task ListsNothingInARepositoryThatHoldsNothing()
    {
        Assert.Empty(await _provider.List(Target, null));
    }

    [Fact]
    public async Task DeletesAnObjectAndSucceedsForOneThatIsNotThere()
    {
        await Put("a.txt", "one"u8.ToArray());
        await Put("b.txt", "two"u8.ToArray());

        await _provider.Delete(Target, "a.txt");
        await _provider.Delete(Target, "a.txt");

        Assert.Equal(["b.txt"], (await _provider.List(Target, null)).Select(stored => stored.Key));
    }

    [Fact]
    public async Task CommitsToTheBranchTheTargetNames()
    {
        var target = Target with { Branch = "objects" };

        using (var content = new MemoryStream("one"u8.ToArray()))
        {
            await _provider.Put(target, "a.txt", content, null);
        }

        Assert.Equal("a.txt", Git(Path.Combine(_remote.Path, "store.git"), "ls-tree", "--name-only", "objects").Trim());
        Assert.Empty(await _provider.List(Target, null));
    }

    [Fact]
    public async Task RefusesARepositoryThatIsNotThere()
    {
        await Assert.ThrowsAsync<BusinessException>(
            () => _provider.List(Target with { Container = "missing.git" }, null));
    }

    [Fact]
    public async Task StoresWhatTheRepositoryIgnoresAndConvertsNothing()
    {
        var seed = Path.Combine(_remote.Path, "seed");

        Git(_remote.Path, "init", "--quiet", "--initial-branch=main", "seed");
        File.WriteAllText(Path.Combine(seed, ".gitignore"), "*.csv\n");
        File.WriteAllText(Path.Combine(seed, ".gitattributes"), "* text eol=crlf\n");
        Git(seed, "add", "--all");
        Git(seed, "-c", "user.name=t", "-c", "user.email=t@t.invalid", "commit", "--quiet", "-m", "seed");
        Git(seed, "push", "--quiet", Path.Combine(_remote.Path, "store.git"), "main");

        var payload = "line\nline\n"u8.ToArray();

        await Put("reports/ledger.csv", payload);

        using var stored = await _provider.Get(Target, "reports/ledger.csv");

        Assert.NotNull(stored);

        using var readBack = new MemoryStream();
        await stored.Content.CopyToAsync(readBack);

        Assert.Equal(payload, readBack.ToArray());
    }

    [Theory]
    [InlineData("a/.git/config")]
    [InlineData(".GIT/x")]
    public async Task RefusesAKeyThatNamesGitMetadataAtAnyDepth(string key)
    {
        using var content = new MemoryStream("x"u8.ToArray());

        await Assert.ThrowsAsync<BusinessException>(() => _provider.Put(Target, key, content, null));
    }

    [Theory]
    [InlineData("--upload-pack=touch x")]
    [InlineData("a..b")]
    public async Task RefusesABranchGitCouldReadAsAnOption(string branch)
    {
        await Assert.ThrowsAsync<BusinessException>(() => _provider.List(Target with { Branch = branch }, null));
    }

    [Fact]
    public async Task RefusesAnAddressThatRunsACommand()
    {
        await Assert.ThrowsAsync<BusinessException>(
            () => _provider.List(Target with { Endpoint = "ext::sh -c 'exit 0'", Container = "x" }, null));
    }

    [Fact]
    public void LinksThroughTheTemplateWithEachSegmentEscaped()
    {
        Assert.Equal(
            new Uri("https://raw.example.test/org/store/main/reports/q1%20final.csv"),
            _provider.Link(Target, "reports/q1 final.csv"));
    }

    [Fact]
    public void HasNoLinkWithoutATemplate()
    {
        Assert.Null(_provider.Link(Target with { LinkTemplate = "" }, "a.txt"));
    }

    async Task Put(string key, byte[] payload)
    {
        using var content = new MemoryStream(payload);

        await _provider.Put(Target, key, content, "text/plain");
    }

    static string Git(string directory, params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = directory, RedirectStandardOutput = true };

        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEnd();

        process.WaitForExit();

        Assert.Equal(0, process.ExitCode);

        return output;
    }

    public void Dispose()
    {
        _remote.Dispose();
    }
}
