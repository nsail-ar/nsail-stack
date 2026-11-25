// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Diagnostics;

namespace NSail.Harness.GitHub;

public sealed class GitHubOptions
{
    public const string Section = "GitHub";

    // The harness's own token, first found of: inline, from a file, `GH_TOKEN`, a workstation's `gh` login.
    // Where it was found travels with it, and a refusal names that, never the token.
    public string? Token { get; set; }
    public string? TokenFile { get; set; }

    public GitHubToken ResolveToken()
    {
        if (!string.IsNullOrWhiteSpace(Token)) return new GitHubToken(Token.Trim(), "GitHub:Token");
        if (!string.IsNullOrWhiteSpace(TokenFile) && File.Exists(TokenFile) && File.ReadAllText(TokenFile) is { } read && !string.IsNullOrWhiteSpace(read))
        {
            return new GitHubToken(read.Trim(), $"GitHub:TokenFile {TokenFile}");
        }
        if (Environment.GetEnvironmentVariable("GH_TOKEN") is { } environment && !string.IsNullOrWhiteSpace(environment))
        {
            return new GitHubToken(environment.Trim(), "GH_TOKEN");
        }
        using var gh = Process.Start(new ProcessStartInfo("gh", "auth token") { RedirectStandardOutput = true, UseShellExecute = false })
            ?? throw new InvalidOperationException("no GitHub token: set GitHub:Token, GitHub:TokenFile or GH_TOKEN, or log in with gh");
        var output = gh.StandardOutput.ReadToEnd().Trim();
        gh.WaitForExit();
        if (gh.ExitCode != 0 || output.Length == 0) throw new InvalidOperationException("no GitHub token: set GitHub:Token, GitHub:TokenFile or GH_TOKEN, or log in with gh");
        return new GitHubToken(output, "the gh login");
    }
}
