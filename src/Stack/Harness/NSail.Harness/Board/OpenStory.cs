// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Board;

// An open story as filed: Author is the login that opened it ("" when the board does not say).
public sealed record OpenStory(int Number, string Title, string Author, IReadOnlySet<string> Labels);
