// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.OpenObserve;

// Where the log store answers and as whom: its address, its organization and logs stream, and a
// reader's sign-in — inline or from a file (a box hands its credentials over as files).
public sealed class OpenObserveOptions
{
    public const string Section = "OpenObserve";

    public string? Endpoint { get; set; }
    public string Organization { get; set; } = "default";
    public string Stream { get; set; } = "default";
    public string? Username { get; set; }
    public string? UsernameFile { get; set; }
    public string? Password { get; set; }
    public string? PasswordFile { get; set; }

    public string? ResolveUsername()
    {
        return Resolve(Username, UsernameFile);
    }

    public string? ResolvePassword()
    {
        return Resolve(Password, PasswordFile);
    }

    static string? Resolve(string? inline, string? file)
    {
        if (!string.IsNullOrWhiteSpace(inline)) return inline.Trim();
        return !string.IsNullOrWhiteSpace(file) && File.Exists(file) ? File.ReadAllText(file).Trim() : null;
    }
}
