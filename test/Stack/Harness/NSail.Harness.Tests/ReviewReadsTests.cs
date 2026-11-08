// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Diagnostics;

namespace NSail.Harness.Tests;

// nsail-stack's review.yml decides which PRs Vigía reads with .github/review-reads.sh: these run it
// on the PR's facts the workflow hands it.
public class ReviewReadsTests
{
    [Theory]
    [InlineData("workflow_run", "false", "story/nsail-3019", "area:stack,bug", true)]
    [InlineData("workflow_run", "false", "stack/nsail-3019", "", true)]
    [InlineData("workflow_run", "false", "feature/try-x", "", false)]
    [InlineData("workflow_run", "false", "story/nsail-3019", "area:stack,hand", false)]
    [InlineData("workflow_run", "false", "story/nsail-3019", "handoff", true)]
    [InlineData("workflow_run", "true", "story/nsail-3019", "", false)]
    [InlineData("workflow_dispatch", "true", "story/nsail-3019", "", false)]
    [InlineData("workflow_dispatch", "false", "feature/try-x", "", true)]
    public void APrIsReadOnItsRepositoryAndItsStoryBranchNotItsAuthor(string evt, string cross, string branch, string labels, bool read)
    {
        // The workflow runs on Linux; a Windows lane's `bash` may be WSL's, which cannot see this path.
        if (OperatingSystem.IsWindows()) return;

        var (code, output) = Run("go", evt, cross, branch, labels);

        Assert.True(code is 0 or 1, output);
        Assert.Equal(read, code == 0);
    }

    [Theory]
    [InlineData("story/nsail-3019", "3019")]
    [InlineData("stack/nsail-3019-retry", "3019")]
    [InlineData("feature/try-x", "")]
    [InlineData("story/nsail-x", "")]
    public void AStoryBranchNamesItsStory(string branch, string story)
    {
        if (OperatingSystem.IsWindows()) return;

        var (code, output) = Run("story", branch);

        Assert.Equal(0, code);
        Assert.Equal(story, output.Trim());
    }

    static (int Code, string Output) Run(params string[] args)
    {
        var start = new ProcessStartInfo("bash") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "review-reads.sh"));
        foreach (var a in args) start.ArgumentList.Add(a);
        using var p = Process.Start(start)!;
        var output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
        p.WaitForExit();
        return (p.ExitCode, output);
    }
}
