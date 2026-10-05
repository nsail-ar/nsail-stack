// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;

namespace NSail.Components;

/// <summary>Answers "may this session open that page?" — the one gate a nav entry, a
/// lookup's + and a contributed row action all ask, so none of them can disagree.
///
/// <para>Each verdict is remembered for the session that earned it, because every caller here
/// asks from inside a render and none of them can cache on its own: <c>NsPageLink</c>
/// re-asks on every parameter set, and a parameter set is every render pass of whatever hosts
/// it — a ten-row list carries one gated link per name and five per row outlet, so one pass
/// over a full page asks sixty times for the same handful of pages. A verdict is a function of
/// the page and the session and of nothing else, so the session is the only thing that earns a
/// fresh ask: the same bargain <see cref="NavMenu.GetItems"/> makes with the tree it builds out
/// of these answers.</para></summary>
public sealed class PageGate(IAuthorizationService authorization, IAuthorizationPolicyProvider policies)
{
    // Keyed by the principal INSTANCE beside the type, so a sign-in or an organization switch is
    // a new key rather than something somebody has to remember to invalidate: the cascaded
    // Task<AuthenticationState> hands out one principal per session, which is the same identity
    // NavMenu keys its own tree on. Concurrent because the dictionary outlives the render that
    // filled it and a scope is a whole WebAssembly session, not one pass.
    readonly ConcurrentDictionary<(ClaimsPrincipal User, Type Type), Task<bool>> _pages = new();

    readonly ConcurrentDictionary<(ClaimsPrincipal User, Type Type), Task<bool>> _messages = new();

    // One instance for the no-session case rather than a fresh principal per call: the key IS
    // the principal, so a new object each time would be a new entry on every render pass of a
    // link that was cascaded no state. Anonymous is anonymous whoever holds the object.
    static readonly ClaimsPrincipal Nobody = new(new ClaimsIdentity());

    /// <summary>Who a cascaded authentication state turns out to be, for a caller that needs
    /// the principal itself because it asks more than one question of it.
    ///
    /// <para>Awaiting the state is ordering as much as it is the user: the client fills its
    /// Session and effective policies while this task completes, and the gate reads those.
    /// Asked any earlier, every page answers "denied". Cascaded nothing, the answer is one
    /// shared anonymous principal and never a fresh one — the remembered verdicts are keyed by
    /// the principal instance, so minting a new object per render pass would fill the session's
    /// dictionary with entries nobody reads a second time.</para></summary>
    public static async Task<ClaimsPrincipal> UserOf(Task<AuthenticationState>? state)
    {
        return state is null ? Nobody : (await state).User;
    }

    public async Task<bool> Allows(Type pageType, Task<AuthenticationState>? state)
    {
        ArgumentNullException.ThrowIfNull(pageType);

        return await Allows(pageType, await UserOf(state));
    }

    public Task<bool> Allows(Type pageType, ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(pageType);
        ArgumentNullException.ThrowIfNull(user);

        return Remembered(_pages, (user, pageType), Resolve);
    }

    /// <summary>Answers "could this session send that message?" — what a nav entry declaring its
    /// own Permission is asked, beside the destination page's gate and never instead of it.</summary>
    public Task<bool> Permits(Type messageType, ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        ArgumentNullException.ThrowIfNull(user);

        return Remembered(_messages, (user, messageType), Fabricated);
    }

    async Task<bool> Resolve((ClaimsPrincipal User, Type Type) key)
    {
        // The page's own attributes, combined and asked the same way AuthorizeRouteView
        // asks them. Reading [Authorize<TMessage>] to call PreAuthorize directly would be
        // a second implementation of the gate, free to disagree with the page it guards.
        var attributes = key.Type.GetCustomAttributes(inherit: true);

        if (attributes.OfType<IAllowAnonymous>().Any())
        {
            return true;
        }

        var policy = await AuthorizationPolicy.CombineAsync(policies, attributes.OfType<IAuthorizeData>());

        if (policy is null)
        {
            return true;
        }

        return (await authorization.AuthorizeAsync(key.User, resource: null, policy)).Succeeded;
    }

    async Task<bool> Fabricated((ClaimsPrincipal User, Type Type) key)
    {
        // Through the host's own policy provider, so the name a page's [Authorize<TMessage>]
        // carries and the one an entry's Permission asks for are fabricated by one piece of
        // code: a drawer that read the grant itself would be free to disagree with the page it
        // opens. A host that registered no message policies answers null here, which is the
        // same "no gate" a page with no attributes gets.
        var policy = await policies.GetPolicyAsync(
            AuthorizeAttribute<object>.MessagePolicyPrefix + key.Type.AssemblyQualifiedName);

        if (policy is null)
        {
            return true;
        }

        return (await authorization.AuthorizeAsync(key.User, resource: null, policy)).Succeeded;
    }

    static Task<bool> Remembered(
        ConcurrentDictionary<(ClaimsPrincipal User, Type Type), Task<bool>> answers,
        (ClaimsPrincipal User, Type Type) key,
        Func<(ClaimsPrincipal User, Type Type), Task<bool>> ask)
    {
        // What is remembered is the TASK and not the verdict, and the decision is taken with no
        // await in front of it: callers arriving while the first ask is still in flight — the
        // ordinary case, since one render pass asks for the same page once per row — join that
        // ask instead of each starting one. A faulted ask is not an answer, so the next caller
        // asks again rather than inheriting the failure the first one met.
        if (answers.TryGetValue(key, out var answer) && !answer.IsFaulted)
        {
            return answer;
        }

        return answers[key] = ask(key);
    }
}
