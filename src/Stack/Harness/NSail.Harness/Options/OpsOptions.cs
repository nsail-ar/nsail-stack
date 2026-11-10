// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness;

// The Ops this Harness reports to. No Url, it reports nowhere.
public sealed class OpsOptions
{
    public string? Url { get; set; }
    public string? Token { get; set; }
    public string? TokenFile { get; set; }
    public int Minutes { get; set; } = 5;

    // The most a report's body weighs, in bytes: under Ops' own ceiling, which refuses rather than cuts.
    // Never less than OpsReporter.MinBytes.
    public int MaxBytes { get; set; } = 60 * 1024;

    public string? ResolveToken()
    {
        return !string.IsNullOrWhiteSpace(Token) ? Token.Trim()
            : !string.IsNullOrWhiteSpace(TokenFile) && File.Exists(TokenFile) ? File.ReadAllText(TokenFile).Trim() : null;
    }
}
