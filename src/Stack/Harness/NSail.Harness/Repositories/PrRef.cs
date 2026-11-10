// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Repositories;

// A story's PR, in whichever of the repositories it was built in; a merged one names its merge commit.
public sealed record PrRef(int Number, string Title, string Head, DateTimeOffset? MergedAt, string Repo, string? MergeSha = null);
