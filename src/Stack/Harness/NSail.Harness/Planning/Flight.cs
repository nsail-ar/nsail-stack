// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Planning;

// What is running already: this harness's own jobs, and the release workflows the code host runs.
public sealed record Flight(
    IReadOnlySet<int> DevStories,
    IReadOnlySet<int> ReviewPrs,
    IReadOnlySet<int> AnalyzeStories,
    bool QaRunning,
    IReadOnlySet<string> ReleaseWorkflows)
{
    // A gate of main's head is running on this machine.
    public bool MainGating { get; init; }

    // The stories a QA walk of this machine is on.
    public IReadOnlySet<int> TestStories { get; init; } = new HashSet<int>();

    // The releases a walk of this machine is shipping, by tag.
    public IReadOnlySet<string> ShipTags { get; init; } = new HashSet<string>();

    public static Flight Empty { get; } = new(new HashSet<int>(), new HashSet<int>(), new HashSet<int>(), false, new HashSet<string>());
}
