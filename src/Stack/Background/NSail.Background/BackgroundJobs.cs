// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Background;

// The job types AddBackgroundJob has collected. One mutable instance held in the service
// collection rather than one registration per job: the runner is added by whichever call
// came first and still sees every job contributed by the calls after it, so the order kits
// compose in never matters.
sealed class BackgroundJobs
{
    readonly List<Type> _types = [];

    public IReadOnlyList<Type> Types => _types;

    public void Add(Type jobType)
    {
        // Composing a kit twice must not give its job two loops running the same work.
        if (!_types.Contains(jobType))
        {
            _types.Add(jobType);
        }
    }
}
