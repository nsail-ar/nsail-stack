// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Data;

// Two kits' jobs of one simple name, each in its kit's namespace: distinct jobs, never siblings.
namespace NSail.Background.Tests.KitOne
{
    sealed class CleanupJob : IBackgroundJob
    {
        readonly Gate _gate;
        readonly Twins _twins;

        public CleanupJob(Gate gate, Twins twins)
        {
            _gate = gate;
            _twins = twins;
        }

        public TimeSpan Interval => TimeSpan.FromHours(1);

        public TenancyScope Tenancy => TenancyScope.Install;

        public async Task Run(CancellationToken cancellationToken)
        {
            _twins.One.Record();

            await _gate.Open.Task.WaitAsync(cancellationToken);
        }
    }
}

namespace NSail.Background.Tests.KitTwo
{
    sealed class CleanupJob : IBackgroundJob
    {
        readonly Gate _gate;
        readonly Twins _twins;

        public CleanupJob(Gate gate, Twins twins)
        {
            _gate = gate;
            _twins = twins;
        }

        public TimeSpan Interval => TimeSpan.FromHours(1);

        public TenancyScope Tenancy => TenancyScope.Install;

        public async Task Run(CancellationToken cancellationToken)
        {
            _twins.Two.Record();

            await _gate.Open.Task.WaitAsync(cancellationToken);
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
}
