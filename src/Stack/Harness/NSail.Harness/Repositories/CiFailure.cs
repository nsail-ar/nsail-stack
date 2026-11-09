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

    // A failed job as the code host reports it: its name, its failed step, and its whole log.
    public static CiFailure Of(IEnumerable<(string Job, string Step, string Log)> jobs)
    {
        var list = jobs.ToList();
        var failed = new HashSet<string>(StringComparer.Ordinal);
        var tail = new List<string>();
        var laid = 0;
        foreach (var (job, step, log) in list)
        {
            var lines = log.Split('\n').Select(Plain).ToList();
            failed.UnionWith(GateJob.FailureSet(string.Join('\n', lines), ""));
            if (laid++ >= TailJobs) continue;
            // A failed step ends at its last error line; what follows is the job's cleanup.
            var end = lines.FindLastIndex(l => l.StartsWith("##[error]", StringComparison.Ordinal));
            var kept = lines.Take(end < 0 ? lines.Count : end + 1).Where(l => l.Length > 0).TakeLast(TailLines);
            tail.Add($"== {job} › {step}");
            tail.AddRange(kept.Select(l => l.Length > LineLength ? l[..LineLength] + " …" : l));
        }
        return new CiFailure(list.Select(j => $"{j.Job} › {j.Step}").ToList(), failed, string.Join('\n', tail));
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
