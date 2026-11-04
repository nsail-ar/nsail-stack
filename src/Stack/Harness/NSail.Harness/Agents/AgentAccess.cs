// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Agents;

// What a seat may do in its folder, said once for every driver; each driver writes it in its own
// tool vocabulary. No seat reaches the code host or pushes, and none starts agents of its own: the
// harness alone acts on what the seat leaves on disk.
public enum AgentAccess
{
    // Reads the tree and its history, writes only its out files (an analyst, a reviewer).
    Reader,

    // Builds: a shell, edits, commits (a developer).
    Builder,

    // Walks a product: reads the tree, writes its out files, drives its MCP servers (a QA walker).
    Walker,
}
