// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness;

// A build turn held: by whom, since when.
public sealed record BuildLease(Guid Id, string Holder, DateTimeOffset At);
