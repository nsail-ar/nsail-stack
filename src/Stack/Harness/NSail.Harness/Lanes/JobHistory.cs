// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Lanes;

// The jobs this machine ran, in one small file of its own: what a status page and a dossier read
// back. It is a log, never state: what the harness decides from lives on the board and the
// repository (labels, stages, comment markers, commit statuses), where every harness sees it. What
// is read back from it to decide is only this machine's own: the agents it spent on a story in a
// day (a worker's DailyCap) and when it last walked one.
public sealed class JobHistory
{
    // How long a job is remembered: long enough to read a week back and to count a day's jobs of a
    // story; the logs keep the rest. It also bounds what is owed to Ops (OpsLedger): a job trimmed
    // before Ops accepts it is lost to the fleet's record.
    public static readonly TimeSpan Keep = TimeSpan.FromDays(7);

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
            var oldest = job.Started - Keep;
            _jobs.RemoveAll(j => j.Started < oldest);
            StateFile.Write(_path, _jobs);
        }
    }

    // How many jobs of a holder (JobContext.HolderOf: a kind of work over a story) ran an agent
    // since then.
    public int Seated(string holder, DateTimeOffset since)
    {
        lock (_gate)
        {
            return _jobs.Count(j => j.Seated && j.Started > since && j.Id.EndsWith($"-{holder}", StringComparison.Ordinal));
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
