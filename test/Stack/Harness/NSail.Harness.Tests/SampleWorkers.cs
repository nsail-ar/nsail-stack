// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Configuration;
using NSail.Harness.Workers;

namespace NSail.Harness.Tests;

// NSail's own workers, as the host ships them: what the planner tests plan with.
static class SampleWorkers
{
    public static readonly HarnessOptions Options = Load();

    // As box1 runs them: the one-machine workers (releases, orphans) on.
    static HarnessOptions Load()
    {
        var o = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "harness-settings.json")).Build()
            .GetSection(HarnessOptions.Section).Get<HarnessOptions>()!;
        foreach (var w in o.Workers.Values.Where(w => !w.Enabled)) w.Enabled = true;
        return o;
    }

    public static readonly IReadOnlyList<Worker> Workers = Worker.Compile(Options);

    public static Plan Plan(BoardSnapshot board, PlannerOptions? options = null)
    {
        return WorkerPlanner.Make(board, Workers, options ?? new PlannerOptions());
    }
}
