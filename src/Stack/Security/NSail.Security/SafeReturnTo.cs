// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Security;

/// <summary>The one guard every door that hands an anonymous visitor's own return address
/// back to a redirect or a link applies before painting it — SignInPage, BookingPage, and
/// the OAuth GoogleState/MetaState/AppleState round-trip a returnUrl/returnTo that never
/// carries a session, so nothing proves it was minted by this install rather than typed into
/// the address bar.</summary>
public static class SafeReturnTo
{
    /// <summary>The value if it is a same-origin path, "/" otherwise — a caller that always
    /// wants somewhere to send a visitor back to.</summary>
    public static string Or(string? value)
    {
        return Parse(value) ?? "/";
    }

    /// <summary>The value if it is a same-origin path, null otherwise.</summary>
    public static string? Parse(string? value)
    {
        // A backslash or a control character (tab, CR, LF) lets a browser's URL parser read a
        // value starting with a single slash as protocol-relative — "/\evil.test" and
        // "/\t/evil.test" both resolve to https://evil.test — so both are rejected alongside
        // the "//" that already reads as protocol-relative on its own.
        return value is { Length: > 0 }
            && value.StartsWith('/')
            && !value.StartsWith("//", StringComparison.Ordinal)
            && !value.Contains('\\', StringComparison.Ordinal)
            && !value.Any(char.IsControl)
                ? value
                : null;
    }
}
