// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Agents;

// QuotaUntil is set when the run died on the account's usage wall: nothing more starts
// before that instant, and the death is no story's fault.
public sealed record AgentResult(int ExitCode, bool TimedOut, DateTimeOffset? QuotaUntil, bool QuotaRefused, string? Result);
