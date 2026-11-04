// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness;

// A board: which provider reads it (its own settings sit beside these, bound by that provider), the
// key its stories are named by (`{key}#{n}`), and the board's own names for the harness's columns
// (Stages) and labels (Labels), each `harness name: board name` — a key ending in `:` maps a prefix.
public sealed class BoardOptions
{
    public string Provider { get; set; } = "GitHub";
    public string? Key { get; set; }
    public Dictionary<string, string> Stages { get; set; } = [];
    public Dictionary<string, string> Labels { get; set; } = [];
}
