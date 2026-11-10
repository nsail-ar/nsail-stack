// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Diagnostics;

namespace NSail.Harness.Tests;

// nsail-stack's review.yml decides the merge from a file in the PR's own tree: these read the
// workflow as it ships, so a verdict the PR commits can never be the one decided on.
public class ReviewVerdictTests : IDisposable
{
    static readonly string[] Workflow = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "review.yml"));

    readonly string _root = Path.Combine(Path.GetTempPath(), "review-verdict-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [Fact]
    public void TheVerdictIsDeletedAfterThePrCheckoutAndBeforeVigia()
    {
        var steps = Steps();
        var head = steps.FindIndex(s => s.Contains("ref: ${{ steps.pr.outputs.sha }}"));
        var clear = steps.FindIndex(s => s.Contains("id: clear"));
        var vigia = steps.FindIndex(s => s.Contains("name: Vigía"));

        Assert.True(head >= 0 && clear > head && vigia > clear, $"PR checkout {head}, clear {clear}, Vigía {vigia}");
        Assert.Contains("run: rm -rf -- \"$FINDINGS\"", steps[clear]);
        Assert.Contains("CLEARED: ${{ steps.clear.outcome }}", steps.Single(s => s.Contains("name: Post and decide")));
    }

    [TheoryOn("unix")]
    [InlineData("success", "VERDICT: findings", "VERDICT: findings")]
    [InlineData("failure", "VERDICT: clean", "")]
    [InlineData("skipped", "VERDICT: clean", "")]
    [InlineData("", "VERDICT: clean", "")]
    [InlineData("success", null, "")]
    public void TheDecisionReadsAVerdictOnlyWhenTheDeleteSucceeded(string cleared, string? written, string verdict)
    {
        Directory.CreateDirectory(_root);
        if (written is not null) File.WriteAllText(Path.Combine(_root, ".vigia-review.md"), written + "\r\nSin hallazgos.\n");
        var read = Workflow.Select(l => l.Trim()).Single(l => l.StartsWith("[ \"$CLEARED\" = success ]", StringComparison.Ordinal));

        var start = new ProcessStartInfo("bash") { WorkingDirectory = _root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("-eo");
        start.ArgumentList.Add("pipefail");
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add($"VERDICT=\"\"\n{read}\nprintf '%s' \"$VERDICT\"");
        start.Environment["CLEARED"] = cleared;
        start.Environment["FINDINGS"] = ".vigia-review.md";
        using var p = Process.Start(start)!;
        var output = p.StandardOutput.ReadToEnd();
        var error = p.StandardError.ReadToEnd();
        p.WaitForExit();

        Assert.True(p.ExitCode == 0, error);
        Assert.Equal(verdict, output);
    }

    // The job's steps, each as its lines joined: a step opens at `      - ` under `steps:`.
    static List<string> Steps()
    {
        var steps = new List<string>();
        foreach (var line in Workflow.SkipWhile(l => l.Trim() != "steps:").Skip(1))
        {
            if (line.StartsWith("      - ", StringComparison.Ordinal)) steps.Add("");
            if (steps.Count > 0) steps[^1] += line.Trim() + "\n";
        }
        return steps;
    }
}
