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

            var touched = SeatOutput.Protected(await SeatOutput.Changed(dir, since), [".github/workflows/**"]);

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
}
