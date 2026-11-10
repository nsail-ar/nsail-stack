// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging.WebApi.Push;

/// <summary>Who hears a push from this scope, and which group a connection opened in it joins —
/// one answer for both ends, so a publish can only reach the connections that stood where it
/// was published. This one is the install's single audience; a host that walls tenants
/// replaces it with the tenant's (<c>AddBaseWebApi</c>).</summary>
public class PushAudience
{
    public virtual string Current
    {
        get { return "install"; }
    }

    /// <summary>What a held feed asks again while it stays open: true while this audience still
    /// has the right to hear. A connection authorizes once at connect, so whatever can be
    /// withdrawn afterwards has to be asked through this, and null — the install's own
    /// audience, which nothing withdraws — is never asked. The check outlives this scope, so
    /// it must open what it reads from.</summary>
    public virtual Func<CancellationToken, Task<bool>>? Watch()
    {
        return null;
    }
}
