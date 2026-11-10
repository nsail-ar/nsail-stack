// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Reflection;
using NSail.Harness.Lanes;
using NSail.Harness.Planning;
using NSail.Injection.Annotations;
using NSail.Messaging;
using NSail.Problems;

namespace NSail.Harness.Api;

// The harness's API: what it shows of itself and the switches a hand flips.
[Injectable]
public sealed class HarnessHandler(HarnessLoop loop, HarnessOptions options, JobHistory history, JobStats stats, BuildLeases leases, LoadSamples load, Agents.IAgent agent) :
    IHandler<GetStatus, HarnessStatus>,
    IHandler<ListJobs, JobsView>,
    IHandler<GetStats, HarnessStatsView>,
    IHandler<PauseHarness>,
    IHandler<ResumeHarness>,
    IHandler<RestartHarness>,
    IHandler<WakeHarness>,
    IHandler<TakeNewStories>,
    IHandler<RunJob, string>,
    IHandler<AcquireBuildLease, Guid>,
    IHandler<ReleaseBuildLease>
{
    public Task<HarnessStatus> Handle(GetStatus message, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new HarnessStatus
        {
            Repository = options.DefaultRepository.Name,
            Machine = options.Machine,
            Version = VersionOf(typeof(HarnessLoop).Assembly),
            HostVersion = Assembly.GetEntryAssembly() is { } host ? VersionOf(host) : null,
            Paused = loop.Paused,
            AgentsOffUntil = loop.AgentsOffUntil,
            Readings = Read(),
            TakeNew = loop.TakeNew,
            AndonStillSince = loop.AndonStillSince,
            Running = [.. loop.Running],
            Last = loop.Last,
        });
    }

    static string? VersionOf(Assembly assembly)
    {
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
    }

    MachineReadings Read()
    {
        var now = DateTimeOffset.UtcNow;
        return new MachineReadings
        {
            Cpu1 = Round(load.Cpu(now, TimeSpan.FromMinutes(1))),
            Cpu5 = Round(load.Cpu(now, TimeSpan.FromMinutes(5))),
            Memory5 = Round(load.Memory(now, TimeSpan.FromMinutes(5))),
            Quota = [.. agent.Usage.Select(w => new QuotaReading { Window = w.Name, Used = Math.Round(w.Utilization * 100, 1), Pace = Math.Round(w.PaceAt(now), 2), ResetsAt = w.ResetsAt })],
        };
    }

    static double? Round(double? value)
    {
        return value is { } v ? Math.Round(v, 1) : null;
    }

    public Task<JobsView> Handle(ListJobs message, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new JobsView
        {
            Running = [.. loop.Running],
            Builds = new BuildsView { Limit = leases.Limit, Held = leases.Holders(), Waiting = leases.Waiting },
            Recent = [.. history.Jobs().TakeLast(Math.Max(0, message.Recent)).Reverse()],
        });
    }

    public Task<HarnessStatsView> Handle(GetStats message, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(stats.View(message.Range, DateTimeOffset.UtcNow));
    }

    public Task Handle(PauseHarness message, CancellationToken cancellationToken = default)
    {
        loop.Pause(string.IsNullOrWhiteSpace(message.Why) ? "a hand" : message.Why);
        return Task.CompletedTask;
    }

    public Task Handle(RestartHarness message, CancellationToken cancellationToken = default)
    {
        loop.Restart(string.IsNullOrWhiteSpace(message.Why) ? "a deploy" : message.Why);
        return Task.CompletedTask;
    }

    public Task Handle(ResumeHarness message, CancellationToken cancellationToken = default)
    {
        loop.Resume();
        return Task.CompletedTask;
    }

    public Task Handle(WakeHarness message, CancellationToken cancellationToken = default)
    {
        loop.Wake();
        return Task.CompletedTask;
    }

    public Task Handle(TakeNewStories message, CancellationToken cancellationToken = default)
    {
        loop.TakeNew = message.Take;
        return Task.CompletedTask;
    }

    public async Task<string> Handle(RunJob message, CancellationToken cancellationToken = default)
    {
        var item = message.Kind switch
        {
            WorkKind.Release when message.Product is { } p => new WorkItem(WorkKind.Release, null, null, "manual", "by hand", ProductName: p),
            WorkKind.Qa when message.Story is not null => new WorkItem(WorkKind.Qa, message.Story, null, "manual", "by hand"),
            WorkKind.Review or WorkKind.Gate when message.Story is not null && message.Pr is not null => new WorkItem(message.Kind, message.Story, message.Pr, "manual", "by hand"),
            WorkKind.Dev or WorkKind.Analyze when message.Story is not null => new WorkItem(message.Kind, message.Story, message.Pr, "manual", "by hand"),
            _ => null,
        };
        if (item is null) throw (BusinessException)BusinessProblem.RuleViolation("harness.run.shape", "A Release takes a product; a Review and a Gate a story and a PR; a Dev and an Analyze a story.");
        return await loop.Force(item) ?? throw (BusinessException)BusinessProblem.InvalidState("no free slot, or the story is locked");
    }

    public async Task<Guid> Handle(AcquireBuildLease message, CancellationToken cancellationToken = default)
    {
        return (await leases.Acquire(message.Holder, message.Rank, cancellationToken)).Id;
    }

    public Task Handle(ReleaseBuildLease message, CancellationToken cancellationToken = default)
    {
        if (!leases.Release(message.Id)) throw (BusinessException)BusinessProblem.NotFound("BuildLease", message.Id);
        return Task.CompletedTask;
    }
}
