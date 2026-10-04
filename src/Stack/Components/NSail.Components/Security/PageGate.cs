// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;

namespace NSail.Components;

/// <summary>Answers "may this session open that page?" — the one gate a nav entry, a
/// lookup's + and a contributed row action all ask, so none of them can disagree.</summary>
public sealed class PageGate(IAuthorizationService authorization, IAuthorizationPolicyProvider policies)
{
    public async Task<bool> Allows(Type pageType, Task<AuthenticationState>? state)
    {
        ArgumentNullException.ThrowIfNull(pageType);

        // Awaiting the state is ordering as much as it is the user: the client fills its
        // Session and effective policies while this task completes, and the gate reads
        // those. Asked any earlier, every page answers "denied".
        var user = state is null
            ? new ClaimsPrincipal(new ClaimsIdentity())
            : (await state).User;

        return await Allows(pageType, user);
    }

    public async Task<bool> Allows(Type pageType, ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(pageType);

        // The page's own attributes, combined and asked the same way AuthorizeRouteView
        // asks them. Reading [Authorize<TMessage>] to call PreAuthorize directly would be
        // a second implementation of the gate, free to disagree with the page it guards.
        var attributes = pageType.GetCustomAttributes(inherit: true);

        if (attributes.OfType<IAllowAnonymous>().Any())
        {
            return true;
        }

        var policy = await AuthorizationPolicy.CombineAsync(policies, attributes.OfType<IAuthorizeData>());

        if (policy is null)
        {
            return true;
        }

        return (await authorization.AuthorizeAsync(user, resource: null, policy)).Succeeded;
    }

    /// <summary>Answers "could this session send that message?" — what a nav entry declaring its
    /// own Permission is asked, beside the destination page's gate and never instead of it.</summary>
    public async Task<bool> Permits(Type messageType, ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(messageType);

        // Through the host's own policy provider, so the name a page's [Authorize<TMessage>]
        // carries and the one an entry's Permission asks for are fabricated by one piece of
        // code: a drawer that read the grant itself would be free to disagree with the page it
        // opens. A host that registered no message policies answers null here, which is the
        // same "no gate" a page with no attributes gets.
        var policy = await policies.GetPolicyAsync(
            AuthorizeAttribute<object>.MessagePolicyPrefix + messageType.AssemblyQualifiedName);

        if (policy is null)
        {
            return true;
        }

        return (await authorization.AuthorizeAsync(user, resource: null, policy)).Succeeded;
    }
}
