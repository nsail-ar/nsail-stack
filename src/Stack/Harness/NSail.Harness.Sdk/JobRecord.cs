// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness;

// A job as the harness remembers it: started, and once it ended, how. Result is the outcome's kind:
// ok, failed (its work went wrong), broke (the harness's own break), provider (the provider's limit, its
// server's failure or a call it did not answer ended it), stopped (the host's stop cancelled it), quiet
// (nothing to do, or the quota wall); null on a job recorded before the harness kept it. Seated: the job ran an agent, which is what a worker's DailyCap counts.
public sealed record JobRecord(string Id, string Work, int? Story, DateTimeOffset Started, DateTimeOffset? Ended, string? Outcome, string LogPath, string? Result = null, bool Seated = false);
