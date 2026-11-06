// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Agents;

public interface IAgent
{
    // The name a story's label gives it (`{driver}:{model}`).
    string Driver { get; }

    Task<AgentResult> Run(AgentRun run, CancellationToken ct);

    // What a script that drives this agent itself needs in its environment (its binary, its
    // credentials, a configuration folder of its own under this name).
    IReadOnlyDictionary<string, string> ScriptEnvironment(string name);

    // Whether such a script's output says it died on the account's usage wall, and until when.
    (bool Refused, DateTimeOffset? Until) Quota(string output);

    // The account's quota windows as the newest run reported them; empty until one has.
    IReadOnlyList<UsageWindow> Usage => [];

    // The longest one command of the agent may run in the foreground: a job silent past it has no
    // command left that could still be running. Null where the agent sets none.
    TimeSpan? LongestCommand => null;
}
