// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging.Annotations;

/// <summary>Caps how often one caller may send this message over HTTP. It belongs on the
/// message and not on a route, because the route is derived from the message already and a
/// second declaration could name a path that no longer exists.
///
/// <para>A message declares it when its endpoint is reachable by somebody the install never
/// authenticated — a door opened by a token in a URL, a webhook. A message behind a policy is
/// already bounded by who holds the grant, and a limit there would only ever refuse a customer
/// mid-work.</para>
///
/// <para>The one ceiling over a message behind a policy is the tenant's request budget, which
/// counts every authenticated send of the tenant rather than one message's: it is a rate the
/// product sells, so the work it refuses is deferred rather than lost, and without it one
/// tenant's flood is spent out of every other tenant's share of the install.</para></summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ThrottledAttribute : Attribute
{
    public ThrottledAttribute(int perMinute)
    {
        PerMinute = perMinute;
    }

    /// <summary>How many sends one caller gets inside a minute. The window is fixed rather than
    /// sliding: a public door is protected by the ceiling, not by the smoothness of it, and a
    /// sliding window keeps a timestamp per request per caller for the privilege.</summary>
    public int PerMinute { get; }

    /// <summary>The name of the property that identifies the account the send is aimed at (a
    /// sign-in's login, a code request's address). When set, the send is also counted per
    /// that value, whatever address it comes from: a per-address ceiling alone is spent only by
    /// one address, so a botnet or a client rotating inside its IPv6 /64 would guess at one
    /// known account without ever meeting it. Must be a public string property read from the body
    /// under its own name (no JsonPropertyName); NSG016 refuses any other.</summary>
    public string? Identity { get; init; }

    /// <summary>How many sends one identity gets inside a minute, from every address together.
    /// Defaults to <see cref="PerMinute"/>; the per-address ceiling stays, so a shop's shared
    /// address is not penalized beyond it.</summary>
    public int IdentityPerMinute { get; init; }
}
