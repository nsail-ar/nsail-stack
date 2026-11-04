// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Planning;

namespace NSail.Harness;

public sealed record RunningJob(string Id, WorkItem Item, int Slot, DateTimeOffset Started, string LogPath);
