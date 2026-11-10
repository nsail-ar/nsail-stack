// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Lanes;

// Which ended jobs this machine's Ops already accepted (ops.json, beside jobs.json): every other
// ended job the list holds is owed. Only an accepted post moves it, so a refused or lost one is
// sent again and Ops keeps each job once. A job trimmed off the list is no longer owed.
public sealed class OpsLedger
{
    readonly string _path;
    readonly JobHistory _history;
    readonly ILogger? _log;
    readonly Lock _gate = new();
    HashSet<JobKey> _accepted;

    public OpsLedger(string root, JobHistory history, ILogger? log = null)
    {
        Directory.CreateDirectory(root);
        _path = Path.Combine(root, "ops.json");
        _history = history;
        _log = log;
        _accepted = [.. StateFile.Read<List<JobKey>>(_path, () => [], log: log)];
    }

    // Oldest ended first.
    public IReadOnlyList<JobRecord> Owed()
    {
        lock (_gate)
        {
            return [.. _history.Jobs().Where(j => j.Ended is not null && !_accepted.Contains(Key(j)))];
        }
    }

    public void Accepted(IEnumerable<JobRecord> jobs)
    {
        lock (_gate)
        {
            var held = _history.Jobs().Select(Key).ToHashSet();
            _accepted = [.. _accepted.Concat(jobs.Select(Key)).Where(held.Contains)];
            try
            {
                StateFile.Write(_path, _accepted.ToList());
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                _log?.LogWarning(e, "state file {Path} could not be written — a restart sends its jobs again", _path);
            }
        }
    }

    static JobKey Key(JobRecord job)
    {
        return new JobKey(job.Id, job.Started);
    }

    sealed record JobKey(string Id, DateTimeOffset Started);
}
