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
}
