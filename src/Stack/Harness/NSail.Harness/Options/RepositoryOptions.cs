// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness;

// A repository a story may be built in: `owner/name` on its provider, the prefix of the branches the
// harness cuts there, and the area label that routes a story to it (none: the default repository).
public sealed class RepositoryOptions
{
    public string Provider { get; set; } = "GitHub";
    public string Name { get; set; } = "";
    public string BranchPrefix { get; set; } = "story";
    public string? Label { get; set; }

    // The branch stories land on and every slot starts from.
    public string DefaultBranch { get; set; } = "main";

    // How the harness builds and tests it; none: the workers that build name a Command instead.
    public BuildOptions? Build { get; set; }

    // How the default repository names the version of this one it builds against; none: this
    // repository's work reaches a release by no pin, and a story built here ships with any release.
    public PinOptions? Pin { get; set; }
}

// A file of the default repository, a pattern whose one group is the version, and the reference
// in this repository that version is (`v{version}`): a release holds a merge here when the
// version its tag pins holds it.
public sealed class PinOptions
{
    public string File { get; set; } = "";
    public string Pattern { get; set; } = "";
    public string Ref { get; set; } = "{version}";
}
