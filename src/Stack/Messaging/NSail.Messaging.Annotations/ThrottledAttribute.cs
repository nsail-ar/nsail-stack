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
/// mid-work.</para></summary>
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
}
