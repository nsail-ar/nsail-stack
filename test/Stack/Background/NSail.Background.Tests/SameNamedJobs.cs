// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Data;

// Two kits' jobs of one simple name, each in its kit's namespace: distinct jobs, never siblings.
// A pair belongs to one test class: the runner's meter has one name for the whole process and a
// reading carries only the job's simple name, so two classes running one pair in parallel read
// each other's gauges.
namespace NSail.Background.Tests.KitOne
{
    // JobLockTests' pair.
    sealed class CleanupJob : TwinJob
    {
        public CleanupJob(Gate gate, Twins twins)
            : base(gate, twins.One)
        {
        }
    }

    // JobMetricsTests' pair.
    sealed class TallyJob : TwinJob
    {
        public TallyJob(Gate gate, Twins twins)
            : base(gate, twins.One)
        {
        }
    }
}

namespace NSail.Background.Tests.KitTwo
{
    sealed class CleanupJob : TwinJob
    {
        public CleanupJob(Gate gate, Twins twins)
            : base(gate, twins.Two)
        {
        }
    }

    sealed class TallyJob : TwinJob
    {
        public TallyJob(Gate gate, Twins twins)
            : base(gate, twins.Two)
        {
        }
    }
}

namespace NSail.Background.Tests
{
    sealed class Twins
    {
        public Signal One { get; } = new();

        public Signal Two { get; } = new();
    }

    // Marks its twin as reached and holds the pass open until the gate is released.
    abstract class TwinJob : IBackgroundJob
    {
        readonly Gate _gate;
        readonly Signal _reached;

        protected TwinJob(Gate gate, Signal reached)
        {
            _gate = gate;
            _reached = reached;
        }

        public TimeSpan Interval => TimeSpan.FromHours(1);

        public TenancyScope Tenancy => TenancyScope.Install;

        public async Task Run(CancellationToken cancellationToken)
        {
            _reached.Record();

            await _gate.Open.Task.WaitAsync(cancellationToken);
        }
    }
}
