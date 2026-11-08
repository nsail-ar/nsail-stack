// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Security.Cryptography;

namespace NSail.Harness;

// Who may drive the harness. The control token (restart, pause, take-new, runs, job logs) is the
// operator's and never reaches a seat; the lease token opens only the build-turn routes and is the one
// credential a lane's environment holds. With no control token configured the control routes stay shut.
public sealed class ControlOptions
{
    public string? Token { get; set; }
    public string? TokenFile { get; set; }

    // Fixed across restarts when set; otherwise a random one per process, handed to every lane.
    public string? LeaseToken { get; set; }

    // Host names accepted besides loopback and the host of PublicUrl (a proxy's name).
    public List<string> Hosts { get; set; } = [];

    readonly Lazy<string> generated = new(() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));

    public string? ResolveToken()
    {
        return !string.IsNullOrWhiteSpace(Token) ? Token.Trim()
            : !string.IsNullOrWhiteSpace(TokenFile) && File.Exists(TokenFile) ? File.ReadAllText(TokenFile).Trim() : null;
    }

    public string ResolveLeaseToken()
    {
        return !string.IsNullOrWhiteSpace(LeaseToken) ? LeaseToken.Trim() : generated.Value;
    }
}
