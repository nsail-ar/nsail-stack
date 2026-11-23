// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness;

// The board's columns, as the pipeline names them: a board that calls them otherwise maps them.
public static class Stages
{
    public const string Backlog = "Backlog";
    public const string Analyze = "Analyze";
    public const string Ready = "Ready";
    public const string InProgress = "In-Progress";
    public const string Merged = "Merged";
    public const string Released = "Released";
    public const string Qa = "QA";
    public const string Closed = "Closed";

    // The stages a story is open in, in the pipeline's order.
    public static readonly string[] Open = [Backlog, Analyze, Ready, InProgress, Merged, Released, Qa];

    // A story out of these has landed: its code rides main.
    public static readonly string[] Landed = [Merged, Released, Qa, Closed];
}
