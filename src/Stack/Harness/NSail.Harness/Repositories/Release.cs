// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Repositories;

// A published release on the code host: its tag, and when it was published.
public sealed record Release(string Tag, DateTimeOffset PublishedAt);
