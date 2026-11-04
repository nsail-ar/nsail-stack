// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness;

// How a repository is built and tested, for the harness's own gate and suites (Builds). Roots: the
// folders whose changes seed projects (empty: the whole tree); a change outside them is prose. A
// change matching Everything is build configuration and builds the whole solution. Tests: the test
// projects the gate runs, by file name; TestFilter narrows them (the browser suites stay out).
public sealed class BuildOptions
{
    public string Kind { get; set; } = "DotNet";
    public string Solution { get; set; } = "";
    public List<string> Roots { get; set; } = [];
    public List<string> Everything { get; set; } = ["*.sln", "*.slnx", "global.json", "*.props", "*.targets"];
    public string Tests { get; set; } = "*.Tests.csproj";
    public string? TestFilter { get; set; }
    public string Configuration { get; set; } = "Release";
    public List<string> RestoreArgs { get; set; } = [];
}
