// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Planning;

namespace NSail.Harness.Jobs;

public interface IJob
{
    WorkItem Item { get; }

    Task<JobOutcome> Run(int slot, string logPath, CancellationToken ct);
}
