// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Security;

/// <summary>What the sign-in address carries when something sent a visitor to it. Every member
/// lives in the Stack because each has its two sides on opposite sides of a boundary: the
/// anonymous bounce (NsNotAuthorized, NSail.Components) and the cookie handler mint the return
/// trip, the sign-in page and every provider's sign-in button read it; the credential wall
/// (NSail.BaseServices.WebApi) marks its challenge and the identity kit's redirect turns that
/// mark into the query the page reads; the identity kit marks the anonymous state a refusal
/// left behind and the bounce turns that into the same query. A second spelling of any of them
/// is a destination dropped in silence, or a person told nothing about why they are back at the
/// door.</summary>
public static class SignInRoutes
{
    public const string ReturnUrlParameter = "returnUrl";

    /// <summary>On the sign-in address when the credential the visitor arrived with named
    /// nobody this install still has. One spelling for every way that happens — a party merged
    /// away, a user disabled, a user deleted — because the only thing the person can act on is
    /// signing in again.</summary>
    public const string StaleParameter = "staleCredential";

    /// <summary>The authentication property the credential wall marks its challenge with, so
    /// the handler that writes the redirect can put StaleParameter on it. A property and not a
    /// flag on HttpContext: it is the carrier the challenge already has, and it reaches the
    /// redirect event typed.</summary>
    public const string StaleProperty = "nsail:stale-credential";

    /// <summary>The claim an authentication state carries when being anonymous is what a refused
    /// credential left behind, so the bounce that writes the address can put StaleParameter on it.
    /// <para>The second arm of the same fact, and the one every product actually ships: a
    /// WebAssembly app does no document navigation after its boot, so a person already inside
    /// meets the refusal as a 401 on a send — the ticket is withdrawn there, and no later
    /// document load has one left to be told about. The state the client rebuilds from that 401
    /// is the only carrier between the refusal and the door, and it is already cascaded to the
    /// bounce. On an unauthenticated identity, which is what keeps the mark from reading as a
    /// claim about somebody.</para></summary>
    public const string StaleClaim = "nsail:credential-refused";
}
