// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Repositories;

// An open, non-draft PR as a whole-repository read sees it: Ci is the CI that runs outside the
// harness (Conflict when the head does not merge), UpdatedAt moves whenever anything on it does,
// GreenAt is when a green head's CI finished (null when it is not green).
public sealed record OpenPr(int Number, string Title, string Head, string Sha, CiState Ci, string Repo, string UpdatedAt, DateTimeOffset? GreenAt = null);
