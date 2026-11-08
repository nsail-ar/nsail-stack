// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Repositories;

// A review or a comment on a PR, and the login that wrote it.
public sealed record PrNote(DateTimeOffset At, string Body, string Author);
