// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Repositories;

// An open, non-draft PR as a whole-repository read sees it: Ci is the CI that runs outside the
// harness (Conflict when the head does not merge), UpdatedAt moves whenever anything on it does.
// HeadRepo is where the head lives and Author who opened it.
public sealed record OpenPr(int Number, string Title, string Head, string Sha, CiState Ci, string Repo, string UpdatedAt, string HeadRepo = "", string Author = "");
