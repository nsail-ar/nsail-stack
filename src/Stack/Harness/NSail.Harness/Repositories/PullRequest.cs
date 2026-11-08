// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Repositories;

public sealed record PullRequest(int Number, string Title, bool Open, string HeadRef, string HeadSha, bool Conflicting, string Body = "", string HeadRepo = "");
