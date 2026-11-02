// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Problems;
using NSail.Storage.Git;
using NSail.Storage.Providers;
using System.Diagnostics;
using System.Text;

namespace NSail.Storage.Tests;

// The provider against a bare repository on the local disk — the remote git speaks to with no
// server — so what is proven is the real executable committing and pushing, not a fake of it. What
// git sends over HTTP, a credential and a cancelled read, is proven against loopback servers.
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

    [Theory]
    [InlineData("a branch")]
    [InlineData("feat..x")]
    [InlineData("release/")]
    public async Task NamesTheBranchFieldWhenTheBranchIsNotAName(string branch)
    {
        var failure = await Assert.ThrowsAsync<BusinessException>(() => _provider.List(Target with { Branch = branch }, null));

        Assert.Contains(failure.Issues, issue => issue.Code.Contains("StorageGitBranchInvalid"));
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

    // A file whose content the remote no longer holds fails any fetch that asks for it, so a read
    // that still answers proves it never asked.
    [Fact]
    public async Task GetsAnObjectWithoutFetchingAnyOtherFile()
    {
        var payload = "the one asked for"u8.ToArray();

        await Put("reports/ledger.csv", payload);
        await Put("backups/large.zip", "never fetched"u8.ToArray());
        ServeFilteredFetches();
        Lose("backups/large.zip");

        using var stored = await _provider.Get(Target, "reports/ledger.csv");

        Assert.NotNull(stored);

        using var readBack = new MemoryStream();
        await stored.Content.CopyToAsync(readBack);

        Assert.Equal(payload, readBack.ToArray());
        Assert.Null(await _provider.Get(Target, "reports/never.csv"));
        Assert.Null(await _provider.Get(Target, "reports"));
    }

    [Fact]
    public async Task ListsAPrefixWithoutFetchingAFileOutsideIt()
    {
        await Put("reports/a.csv", "12345"u8.ToArray());
        await Put("reports/b.csv", "1"u8.ToArray());
        await Put("backups/c.zip", "never fetched"u8.ToArray());
        ServeFilteredFetches();
        Lose("backups/c.zip");

        var listed = await _provider.List(Target, "reports/");

        Assert.Equal(["reports/a.csv", "reports/b.csv"], listed.Select(stored => stored.Key));
        Assert.Equal([5L, 1L], listed.Select(stored => stored.Length));
    }

    [Fact]
    public async Task ReadsEveryLengthFromAServerThatFiltersAndOneThatDoesNot()
    {
        await Put("reports/a.csv", "12345"u8.ToArray());
        await Put("backups/c.zip", "123"u8.ToArray());

        var unfiltered = await _provider.List(Target, "reports/");

        ServeFilteredFetches();

        Assert.Equal([5L], unfiltered.Select(stored => stored.Length));
        Assert.Equal([5L], (await _provider.List(Target, "reports/")).Select(stored => stored.Length));
        Assert.Equal([3L, 5L], (await _provider.List(Target, null)).Select(stored => stored.Length));
    }

    [Fact]
    public async Task SendsTheCredentialToNoOriginARedirectNames()
    {
        using var elsewhere = new Loopback(Loopback.EmptyRepository);
        using var configured = new Loopback(request => Loopback.Redirect($"{elsewhere.Origin}{request.Path}"));

        var failure = await Record.ExceptionAsync(() => _provider.List(Credentialed(configured), null));

        Assert.DoesNotContain(elsewhere.Requests, request => request.Authorization is not null);
        Assert.Contains(configured.Requests, request => request.Authorization is not null);
        Assert.IsType<BusinessException>(failure);
    }

    [Fact]
    public async Task ACancelledCallKillsGitAndLeavesNoReadUnobserved()
    {
        using var silent = new Loopback(null);
        var unobserved = new List<Exception>();
        EventHandler<UnobservedTaskExceptionEventArgs> record = (_, args) =>
        {
            lock (unobserved)
            {
                unobserved.Add(args.Exception);
            }
        };

        TaskScheduler.UnobservedTaskException += record;

        try
        {
            using var cancel = new CancellationTokenSource();
            var listing = _provider.List(Credentialed(silent), null, cancel.Token);

            // Cut once the server holds git's request, so what is cancelled is a read off the network.
            await silent.Held.WaitAsync(TimeSpan.FromSeconds(60));

            var clock = Stopwatch.StartNew();

            cancel.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => listing);

            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(30), $"the call outlived its cancellation by {clock.Elapsed}");

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.Empty(unobserved);
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= record;
        }
    }

    static StorageTarget Credentialed(Loopback server)
    {
        return new StorageTarget("git", server.Origin, "", "store.git", "user", "token", "", "");
    }

    // The bare remote answers a filtered fetch and a fetch by id, as a hosted one does.
    void ServeFilteredFetches()
    {
        var bare = Path.Combine(_remote.Path, "store.git");

        Git(bare, "config", "uploadpack.allowFilter", "true");
        Git(bare, "config", "uploadpack.allowAnySHA1InWant", "true");
    }

    // A small push arrives as loose objects, one file per object.
    void Lose(string key)
    {
        var bare = Path.Combine(_remote.Path, "store.git");
        var blob = Git(bare, "rev-parse", $"main:{key}").Trim();
        var file = Path.Combine(bare, "objects", blob[..2], blob[2..]);

        File.SetAttributes(file, FileAttributes.Normal);
        File.Delete(file);
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
