// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Jobs;

// What one job runs, resolved from the worker that planned it over its type's defaults: the seat
// (an agent job), the command (a build, a suite, a drain), its age limit and its own variables.
public sealed record JobStep(WorkerOptions Worker, AgentOptions? Agent, CommandOptions? Command, int Minutes, IReadOnlyDictionary<string, string> Variables);
