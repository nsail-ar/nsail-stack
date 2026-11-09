// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.RegularExpressions;
using NSail.Harness.Jobs;

namespace NSail.Harness.Repositories;

// What failed in the CI that runs outside the harness: each failed job with its failed step, the
// failed tests by name (else the build errors), comparable across runs as the gate's are, and the end
// of each failed step's log.
public sealed partial record CiFailure(IReadOnlyList<string> Steps, IReadOnlySet<string> Failed, string Tail)
{
    const int TailLines = 40;
    const int LineLength = 300;
    const int TailJobs = 3;

    // One failed job as the code host reports it, reduced at once: its whole log is never kept.
    public static CiFailure Of(string job, string step, string log)
    {
        var lines = log.Split('\n').Select(Plain).ToList();
        var failed = GateJob.FailureSet(string.Join('\n', lines), "");
        var end = EndOfStep(lines);
        var kept = lines.Take(end < 0 ? lines.Count : end + 1).Where(l => l.Length > 0).TakeLast(TailLines);
        var tail = new List<string> { $"== {job} › {step}" };
        tail.AddRange(kept.Select(l => l.Length > LineLength ? l[..LineLength] + " …" : l));
        return new CiFailure([$"{job} › {step}"], failed, string.Join('\n', tail));
    }

    // Every failed job of a head or of main, the tails of the first few only.
    public static CiFailure Of(IEnumerable<CiFailure> jobs)
    {
        var list = jobs.ToList();
        return new CiFailure(list.SelectMany(j => j.Steps).ToList(), list.SelectMany(j => j.Failed).ToHashSet(StringComparer.Ordinal),
            string.Join('\n', list.Take(TailJobs).Select(j => j.Tail)));
    }

    public static CiFailure Of(IEnumerable<(string Job, string Step, string Log)> jobs)
    {
        return Of(jobs.Select(j => Of(j.Job, j.Step, j.Log)));
    }

    // The failed step is the job's first: it closes on the runner's exit-code error, and a later step
    // that runs anyway (an upload, a post step) is not its tail. Without that line, the last error.
    static int EndOfStep(List<string> lines)
    {
        var exit = lines.FindIndex(l => l.StartsWith("##[error]Process completed with exit code", StringComparison.Ordinal));
        return exit >= 0 ? exit : lines.FindLastIndex(l => l.StartsWith("##[error]", StringComparison.Ordinal));
    }

    // The failed tests' names, else the build errors, as a builder reads them.
    public IEnumerable<string> Names => Failed.Order(StringComparer.Ordinal).Select(f => f.StartsWith("Failed ", StringComparison.Ordinal) ? f["Failed ".Length..] : f);

    // A red that is all main's own: something named failed, and main's newest completed CI failed it too.
    public bool SharedWith(CiFailure? main)
    {
        return Failed.Count > 0 && main is not null && Failed.IsSubsetOf(main.Failed);
    }

    // A log line without the code host's timestamp.
    static string Plain(string line)
    {
        return Stamp().Replace(line.TrimEnd('\r').TrimStart('\uFEFF'), "");
    }

    [GeneratedRegex(@"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(\.\d+)?Z ")] private static partial Regex Stamp();
}
