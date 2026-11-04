// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Agents;

// One headless run of a coding agent in a prepared folder. Driver-neutral: the job around it never
// knows which vendor's CLI runs it. Model is a name the driver resolves (an alias for its newest
// model, or a full id). Marketplace is the checkout of main whose extensions the driver installs
// before the run, if its own configuration names any. Mcp: a file naming the MCP servers the run has;
// Tools: what it may call beyond its access.
public sealed record AgentRun(
    string Prompt,
    string WorkDir,
    string Model,
    AgentAccess Access,
    int MaxTurns,
    TimeSpan Timeout,
    IReadOnlyDictionary<string, string> Env,
    string LogPath,
    string? Marketplace = null,
    string? Mcp = null,
    IReadOnlyList<string>? Tools = null);
