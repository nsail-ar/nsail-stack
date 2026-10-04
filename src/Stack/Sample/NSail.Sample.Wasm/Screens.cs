// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using NSail.Components;
using NSail.Localization;

namespace NSail.Sample;

public static class Screens
{
    public static void AddSampleComponents(this IServiceCollection services)
    {
        services.AddRoutesFromAssembly();
        services.AddStringsFromAssembly();
        services.AddNavMenu<Menu>();

        // NsPage asks for a signed-in user by default, which an app with no sign-in can never
        // have: here the default is anyone, and what still guards a page is its message grant
        // ([Authorize<TMessage>]), read against the built-in policy like on any host.
        services.AddAuthorizationCore(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder()
                .RequireAssertion(_ => true)
                .Build();
        });

        // The router asks every page for its grant through the authentication state, so the
        // state has to exist even where nobody can sign in: always the anonymous visitor.
        services.AddCascadingAuthenticationState();
        services.AddScoped<AuthenticationStateProvider, AnonymousAuthenticationStateProvider>();
    }
}
