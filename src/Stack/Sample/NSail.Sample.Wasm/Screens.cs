// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using NSail.Components;
using NSail.Exporting;
using NSail.Exporting.Excel;
using NSail.Localization;

namespace NSail.Sample;

public static class Screens
{
    public static void AddSampleComponents(this IServiceCollection services)
    {
        services.AddRoutesFromAssembly();
        services.AddStringsFromAssembly();
        services.AddNavMenu<Menu>();

        // Which files this install's lists export, and the host's call because the spreadsheet
        // half costs a library: a grid offers the kinds that are registered and nothing else,
        // so leaving the second line out is how an install serves only the text file.
        services.AddExporting();
        services.AddExcelExport();

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
