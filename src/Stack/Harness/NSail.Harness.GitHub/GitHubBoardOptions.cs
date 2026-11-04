// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.GitHub;

// A board on GitHub, from Harness:Board: the repository whose issues are the stories, and the
// project (by number, of the repository's owner) whose StatusField is each story's column.
public sealed class GitHubBoardOptions
{
    public string Repository { get; set; } = "";
    public int Project { get; set; }
    public string StatusField { get; set; } = "Status";
}
