// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Jobs;
using NSail.Harness.Lanes;

namespace NSail.Harness.Tests;

public class SeatOutputTests
{
    [Fact]
    public void ALabelOutsideTheKnownSetOrOneASeatMayNotSetIsDropped()
    {
        var known = new[] { "area:shop", "claude:opus", "p1", "claimed", "hand", "stalled" };
        var allowed = SeatOutput.AllowedLabels(["area:shop", "p1", "claimed", "hand", "stalled", "made-up", " claude:opus "], known);
        Assert.Equal(["area:shop", "claude:opus"], allowed);
    }

    [Theory]
    [InlineData(".github/workflows/ci.yml", true)]
    [InlineData(".github/workflows/sub/x.yml", true)]
    [InlineData(".github/voyage.sh", false)]
    [InlineData("src/.github/workflows/ci.yml", false)]
    public void AProtectedGlobMatchesItsPaths(string file, bool protectedPath)
    {
        Assert.Equal(protectedPath ? 1 : 0, SeatOutput.Protected([file], [".github/workflows/**"]).Count);
    }

    [Fact]
    public async Task AScreenWithNoMainThrowsRatherThanSeeingNothing()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => SeatOutput.Changed(Path.GetTempPath(), "HEAD", ""));
    }

    [Fact]
    public async Task TheScreenSeesQuotedNamesAndTheSourceOfARename()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"seat-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, ".github", "workflows"));
        try
        {
            await Shell.Git(dir, "init", "-q");
            await Shell.Git(dir, "config", "user.name", "t");
            await Shell.Git(dir, "config", "user.email", "t@example.com");
            await Shell.Git(dir, "config", "commit.gpgsign", "false");
            await File.WriteAllTextAsync(Path.Combine(dir, ".github", "workflows", "ci.yml"), "name: ci\non: push\njobs: {}\n");
            await Shell.Git(dir, "add", "-A");
            await Shell.Git(dir, "commit", "-q", "-m", "base");
            var since = (await Shell.Git(dir, "rev-parse", "HEAD")).Trim();
            await Shell.Git(dir, "mv", ".github/workflows/ci.yml", ".github/ci.yml");
            await File.WriteAllTextAsync(Path.Combine(dir, ".github", "workflows", "caf\u00e9x.yml"), "a\n");
            if (!OperatingSystem.IsWindows())
            {
                await File.WriteAllTextAsync(Path.Combine(dir, ".github", "workflows", "q\"a\nb.yml"), "a\n");
            }
            await Shell.Git(dir, "add", "-A");
            await Shell.Git(dir, "commit", "-q", "-m", "work");

            var touched = SeatOutput.Protected(await SeatOutput.Changed(dir, since, since), [".github/workflows/**"]);

            Assert.Contains(".github/workflows/ci.yml", touched);
            Assert.Contains(".github/workflows/caf\u00e9x.yml", touched);
            if (!OperatingSystem.IsWindows())
            {
                Assert.Contains(touched, t => t.StartsWith(".github/workflows/q\"a", StringComparison.Ordinal));
            }
        }
        finally
        {
            foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
            Directory.Delete(dir, true);
        }
    }

    // A repository whose main and story branch both start from a commit holding ci.yml; the branch is checked out.
    static async Task<string> Forked()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"seat-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, ".github", "workflows"));
        await Shell.Git(dir, "init", "-q", "-b", "main");
        await Shell.Git(dir, "config", "user.name", "t");
        await Shell.Git(dir, "config", "user.email", "t@example.com");
        await Shell.Git(dir, "config", "commit.gpgsign", "false");
        await Write(dir, ".github/workflows/ci.yml", "on: push\n", "base");
        await Shell.Git(dir, "checkout", "-q", "-b", "story");
        return dir;
    }

    static async Task Write(string dir, string path, string text, string message)
    {
        var file = Path.Combine(dir, path);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        await File.WriteAllTextAsync(file, text);
        await Shell.Git(dir, "add", "-A");
        await Shell.Git(dir, "commit", "-q", "-m", message);
    }

    static void Delete(string dir)
    {
        foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
        Directory.Delete(dir, true);
    }

    [Fact]
    public async Task AWorkflowMergedInFromMainAsMainHoldsItIsNotTheBranchs()
    {
        var dir = await Forked();
        try
        {
            var since = await Shell.Git(dir, "rev-parse", "HEAD");
            await Shell.Git(dir, "checkout", "-q", "main");
            await Write(dir, ".github/workflows/ci.yml", "on: pull_request\n", "main's workflow");
            await Write(dir, ".github/workflows/release.yml", "on: push\n", "main's new workflow");
            var main = await Shell.Git(dir, "rev-parse", "HEAD");
            await Shell.Git(dir, "checkout", "-q", "story");
            await Shell.Git(dir, "merge", "-q", "--no-edit", "main");
            await Write(dir, "src/Foo.cs", "class Foo;\n", "work");

            var changed = await SeatOutput.Changed(dir, since, main);

            Assert.Empty(SeatOutput.Protected(changed, [".github/workflows/**"]));
            Assert.Contains("src/Foo.cs", changed);
        }
        finally
        {
            Delete(dir);
        }
    }

    [Fact]
    public async Task AWorkflowAMergeResolvesToContentMainDoesNotHoldIsTheBranchs()
    {
        var dir = await Forked();
        try
        {
            // An earlier voyage's workflow edit, already pushed: this voyage starts after it.
            await Write(dir, ".github/workflows/ci.yml", "on: [push]\n", "the branch's workflow");
            var since = await Shell.Git(dir, "rev-parse", "HEAD");
            await Shell.Git(dir, "checkout", "-q", "main");
            await Write(dir, ".github/workflows/ci.yml", "on: pull_request\n", "main's workflow");
            var main = await Shell.Git(dir, "rev-parse", "HEAD");
            await Shell.Git(dir, "checkout", "-q", "story");
            await Shell.Run("git", ["merge", "-q", "--no-edit", "main"], dir);
            await File.WriteAllTextAsync(Path.Combine(dir, ".github", "workflows", "ci.yml"), "on: [push, pull_request]\n");
            await Shell.Git(dir, "add", "-A");
            await Shell.Git(dir, "commit", "-q", "--no-edit");

            Assert.Equal([".github/workflows/ci.yml"], SeatOutput.Protected(await SeatOutput.Changed(dir, since, main), [".github/workflows/**"]));
        }
        finally
        {
            Delete(dir);
        }
    }
}
