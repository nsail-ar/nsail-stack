// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Lanes;

namespace NSail.Harness.Builds;

// What builds and tests a repository for the harness: its gate over what a change can affect, and a
// suite run by project and filter. Each run's output is one ShellResult, as a command's would be.
public interface IBuilder
{
    // The projects a change since base can affect, repository-relative; null when no narrower answer
    // can be proven (no base, build configuration changed), empty when nothing builds.
    Task<IReadOnlyList<string>?> Affected(string dir, string? baseSha, CancellationToken ct = default);

    // Restore, build what the change affects, test the affected test projects; as runAs when it is set.
    Task<ShellResult> Gate(string dir, string? baseSha, IDictionary<string, string> env, TimeSpan timeout, TextWriter log, CancellationToken ct = default, string? runAs = null);

    // One test project (a browser suite) built and run, its tests narrowed by the filter.
    Task<ShellResult> Suite(string dir, string project, string? filter, IDictionary<string, string> env, TimeSpan timeout, TextWriter log, CancellationToken ct = default, string? runAs = null);
}
