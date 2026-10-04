// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging.Runtime.Publishing;

/// <summary>The client's end of the server's push: while it is open, every <c>[Pushed]</c>
/// event the server publishes in this tenant is published again through this scope's
/// Mediator. The chrome opens it for a signed-in session and closes it on sign-out; nothing
/// else touches it, and no screen knows it exists.
///
/// <para>This one is closed for good, and it is what a host with no push transport composes —
/// the server, which prerenders and is the end that publishes, among them.</para></summary>
public class PushFeed
{
    /// <summary>Where the server listens, relative to the host's own origin. Under <c>api/</c>
    /// so an unauthenticated connect is answered 401 rather than redirected to sign-in.</summary>
    public const string Path = "api/push";

    /// <summary>The one method a client listens for: the event's type name and its body in the
    /// wire's own JSON, so the hub's protocol never has to know an NSail type. Held here because
    /// both ends read it and the server's assembly is one a WebAssembly client cannot take.</summary>
    public const string Method = "Pushed";

    /// <summary>Starts listening, and keeps trying until it is closed. Idempotent, and it
    /// returns at once: a connection that is slow to come up must not hold the screen.</summary>
    public virtual void Open()
    {
    }

    public virtual Task Close()
    {
        return Task.CompletedTask;
    }
}
