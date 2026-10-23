// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Text;

/// <summary>Whether a written address may be drawn as a door. Down here because every surface
/// that draws an address it did not DERIVE has to ask — the markdown renderer over a document's
/// own links, the drawer over a module's external entry — and a second copy of the rule is a
/// second answer: a "javascript:" href is the whole reason an address that arrives written is
/// never trusted on its own word.</summary>
public static class WebAddress
{
    static readonly string[] Schemes = ["http://", "https://", "mailto:"];

    /// <summary>True for one of the three schemes that open a page or a letter, and — where the
    /// surface admits this app's own addresses — for a root-relative path, never the
    /// protocol-relative "//host" that reads like one and names somebody else. A rejected
    /// address is not repaired: what its author meant by it would be a guess.</summary>
    public static bool IsNavigable(string? address, bool withinApp)
    {
        // A browser strips whitespace and control characters before it reads the scheme, so
        // "java\tscript:alert(1)" IS the javascript scheme to whoever clicks it: an address
        // carrying either is refused rather than trimmed, since the value drawn is the one that
        // arrived and not a cleaned copy of it.
        if (string.IsNullOrEmpty(address) || address.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            return false;
        }

        if (withinApp && address.StartsWith('/') && !address.StartsWith("//", StringComparison.Ordinal))
        {
            return true;
        }

        return Schemes.Any(scheme => address.StartsWith(scheme, StringComparison.OrdinalIgnoreCase));
    }
}
