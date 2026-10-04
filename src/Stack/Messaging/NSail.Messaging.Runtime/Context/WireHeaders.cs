// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging.Runtime.Context;

/// <summary>The shape a delivery's headers take on a transport hop: one prefix both ends agree
/// on, so the name a caller passed a Send is the name the far side reads. Values are already
/// strings and HTTP matches names case insensitively, exactly as <see cref="MessageContext"/>
/// does, so nothing about the round trip is invented.</summary>
public static class WireHeaders
{
    public const string Prefix = "X-NSail-";

    /// <summary>Spoken for by the transport itself: the proxy strips the incoming copy of the
    /// tenant header and writes its own (data-tenancy.md), so a delivery's header of this name would
    /// shadow the install's tenancy word going out and invent one coming in. Reserved on both
    /// ends rather than filtered on one, so what a caller stamps is what the far side reads.
    /// This is a reserved name, not an allowlist — every other header crosses.</summary>
    public const string Tenant = Prefix + "Tenant";

    /// <summary>Spoken for by the transport too: every response carries the build that answered
    /// it, which is how a client left open across a deploy learns the server moved. A delivery
    /// of this name would reach the far side wearing the transport's own word while being
    /// caller input, so the name means one thing on both legs of a hop.</summary>
    public const string Version = Prefix + "Version";

    /// <summary>The wire name a header travels under, null when it does not travel at all: an
    /// unnamed header, or one whose wire name the transport reserves.</summary>
    public static string? Name(string header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return null;
        }

        var name = Prefix + header;

        return IsReserved(name) ? null : name;
    }

    /// <summary>The delivery's own name behind a wire name, null when the header is not a
    /// delivery's or is reserved by the transport.</summary>
    public static string? Logical(string name)
    {
        if (string.IsNullOrEmpty(name)
            || !name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
            || IsReserved(name))
        {
            return null;
        }

        var logical = name[Prefix.Length..];

        return logical.Length == 0 ? null : logical;
    }

    static bool IsReserved(string name)
    {
        return string.Equals(name, Tenant, StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, Version, StringComparison.OrdinalIgnoreCase);
    }
}
