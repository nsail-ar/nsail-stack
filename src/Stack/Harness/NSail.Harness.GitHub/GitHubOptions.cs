// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Diagnostics;

namespace NSail.Harness.GitHub;

public sealed class GitHubOptions
{
    public const string Section = "GitHub";

    // The harness's own token: inline, from a file, or the environment; a workstation's `gh` login otherwise.
    public string? Token { get; set; }
    public string? TokenFile { get; set; }

    public string ResolveToken()
    {
        var token = Token
            ?? (!string.IsNullOrWhiteSpace(TokenFile) && File.Exists(TokenFile) ? File.ReadAllText(TokenFile) : null)
            ?? Environment.GetEnvironmentVariable("GH_TOKEN");
        if (!string.IsNullOrWhiteSpace(token)) return token.Trim();
        using var gh = Process.Start(new ProcessStartInfo("gh", "auth token") { RedirectStandardOutput = true, UseShellExecute = false })
            ?? throw new InvalidOperationException("no GitHub token: set GitHub:Token, GitHub:TokenFile or GH_TOKEN, or log in with gh");
        var output = gh.StandardOutput.ReadToEnd().Trim();
        gh.WaitForExit();
        if (gh.ExitCode != 0 || output.Length == 0) throw new InvalidOperationException("no GitHub token: set GitHub:Token, GitHub:TokenFile or GH_TOKEN, or log in with gh");
        return output;
    }
}
