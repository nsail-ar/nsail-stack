// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Authorization;

namespace NSail.Components;

/// <summary>Declares that a page is visible only to a session that could send TMessage —
/// the entry message that drives the page's visibility. It is a real ASP.NET
/// AuthorizeAttribute (AuthorizeRouteView reads it natively, and it composes with plain
/// [Authorize]/[AllowAnonymous]); the message type travels in Policy, and the app's
/// IAuthorizationPolicyProvider turns it into a PreAuthorize check against SecurityManager.
/// Multiple attributes stack (all must pass).</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class AuthorizeAttribute<TMessage> : AuthorizeAttribute
{
    /// <summary>Policy names carrying a message type start with this, so the provider can
    /// tell them from ordinary named policies and delegate the rest to the default.</summary>
    public const string MessagePolicyPrefix = "msg:";

    public AuthorizeAttribute()
    {
        Policy = MessagePolicyPrefix + typeof(TMessage).AssemblyQualifiedName;
    }
}
