// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Repositories;

namespace NSail.Harness.Planning;

// A story's open PR as the planner weighs it. Word is the reviewer's last verdict at Sha (clean,
// findings), null when nobody read that head; Gate is the harness's own build verdict at Sha, null
// when none ran there yet.
public sealed record StoryPr(int Story, int Number, string Sha, CiState Ci, string Repo, string? Word, CiState? Gate = null);
