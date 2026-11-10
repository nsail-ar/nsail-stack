// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness;

// A command a step runs in a checkout: a process (`dotnet`, a tool), or a script run by its
// extension's interpreter (.sh under bash, .ps1 under PowerShell). The step may add arguments after Args.
public sealed class CommandOptions
{
    public string Run { get; set; } = "";
    public List<string> Args { get; set; } = [];

    public override string ToString()
    {
        return string.Join(' ', [Run, .. Args]);
    }
}
