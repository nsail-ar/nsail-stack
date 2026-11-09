// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Lanes;

// The jobs this machine ran, in one small file of its own: what a status page and a dossier read
// back. It is a log, never state: what the harness decides from lives on the board and the
// repository (labels, stages, comment markers, commit statuses), where every harness sees it.
public sealed class JobHistory
{
    readonly string _path;
    readonly Lock _gate = new();
    readonly List<JobRecord> _jobs;

    public JobHistory(string root, ILogger? log = null)
    {
        Directory.CreateDirectory(root);
        _path = Path.Combine(root, "jobs.json");
        _jobs = StateFile.Read<List<JobRecord>>(_path, () => [], log: log);
    }

    public void Record(JobRecord job)
    {
        lock (_gate)
        {
            _jobs.RemoveAll(j => j.Id == job.Id);
            _jobs.Add(job);
            // The newest few hundred are enough to read what happened; the logs keep the rest.
            if (_jobs.Count > 500) _jobs.RemoveRange(0, _jobs.Count - 500);
            StateFile.Write(_path, _jobs);
        }
    }

    public IReadOnlyList<JobRecord> Jobs()
    {
        lock (_gate)
        {
            return _jobs.ToList();
        }
    }
}
