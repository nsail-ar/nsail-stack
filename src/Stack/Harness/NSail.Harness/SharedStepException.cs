// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness;

// A step every job shares failed (the mirror's fetch): the fault is the harness's, never the story's.
// Probe runs the step again, for the pause it caused to lift on the first pass it succeeds.
public sealed class SharedStepException(string step, string detail, Func<Task> probe, Exception inner)
    : Exception($"{step} failed: {detail}", inner)
{
    public string Step => step;

    public Task Probe()
    {
        return probe();
    }
}
