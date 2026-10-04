// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging.Annotations;

/// <summary>A server-side publish of this event also reaches every client signed in to the same
/// tenant, where it is published again through the client's own Mediator — so a screen hears
/// it with the same <c>Subscribe</c> it uses for an event raised on the client, and never
/// learns that a socket carried it. Read at compile time only: the kit's SignalR.Hubs and
/// SignalR.Clients holders register the two ends for every message carrying it.
///
/// <para>Everyone in the tenant hears it, whatever branch their seat stands in. So a pushed
/// event carries no row and no words: it says that something moved, and the client re-reads
/// through its own org-scoped read whatever it decides to.</para></summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class PushedAttribute : Attribute
{
}
