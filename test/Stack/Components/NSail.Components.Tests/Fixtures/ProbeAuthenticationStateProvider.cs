// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using NSail.Security;

namespace NSail.Components.Tests.Fixtures;

/// <summary>The shape IamAuthenticationStateProvider has: one resolved answer and a Refresh
/// that drops it and tells the tree. Sign out is exactly that call followed by a navigation,
/// so a test driving this drives the real flip.</summary>
public sealed class ProbeAuthenticationStateProvider : AuthenticationStateProvider
{
    ClaimsPrincipal _user = new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "amina")], "probe"));

    // ONE resolved answer, handed back by reference until it is dropped — the real provider's
    // own shape, and the shape the framework's cascade has on top of it. A fresh task per ask
    // would be a session that looks new to everything keyed on it every time it is looked at.
    Task<AuthenticationState>? _state;

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        return _state ??= Task.FromResult(new AuthenticationState(_user));
    }

    public void SignOut()
    {
        Flip(new ClaimsIdentity());
    }

    /// <summary>The same flip, where being anonymous is what a refused credential left behind:
    /// the identity kit marks the state it rebuilds from a 401 (SignInRoutes.StaleClaim), and that
    /// mark is the only thing standing between the refusal and what the door can say.</summary>
    public void CredentialRefused()
    {
        Flip(new ClaimsIdentity([new Claim(SignInRoutes.StaleClaim, "1")]));
    }

    void Flip(ClaimsIdentity identity)
    {
        _user = new ClaimsPrincipal(identity);
        _state = null;

        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}
