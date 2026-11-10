// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Planning;

// A red fix ready to be cut: the reader already checked every condition the cut needs.
public sealed record ReleaseCandidate(int Story, string Workflow, string Sha);
