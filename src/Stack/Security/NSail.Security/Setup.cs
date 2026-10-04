// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSail.Messaging.Runtime;
using NSail.Messaging.Runtime.Pipelines;

namespace NSail.Security;

public static class Setup
{
    /// <summary>TryAdds the security services with their permissive defaults (anonymous
    /// SessionProvider, optimistic RelationProvider and CredentialProvider) — a product without
    /// Iam still runs.</summary>
    public static void AddSecurity(this IServiceCollection services)
    {
        services.AddMessageRegistry();
        services.TryAddScoped<SessionProvider>();
        services.TryAddScoped<RelationProvider>();
        services.TryAddScoped<CredentialProvider>();
        services.TryAddScoped<RequestOrganizationProvider>();
        services.TryAddScoped<SecurityManager>();
    }

    /// <summary>Turns the gate on: every Mediator send is authorized (deny by default).
    /// Server-side only — the client stays permissive and lets the server refuse.</summary>
    public static void AddSecurityEnforcement(this IServiceCollection services)
    {
        services.AddSecurity();
        services.AddSingleton<SecurityEnforcement>();
        services.AddScoped(typeof(IInterceptor<>), typeof(SecurityInterceptor<>));
        services.AddScoped(typeof(IInterceptor<,>), typeof(SecurityInterceptor<,>));
    }

    /// <summary>Registers a code-owned policy: immune to administration (no deleted row
    /// can lock anyone out; no misclick can widen it), shown read-only in the editor.
    /// <para>Its Name is a localization key, not prose: the editor lists it beside rows a
    /// shop wrote in its own language, and a kit that carried one language's sentence here
    /// could only ever be read in that one. The English lives in the registering project's
    /// strings.json and the translations beside it — the file the SERVER composition
    /// registers, because the list handler is what resolves the key.</para></summary>
    public static void AddBuiltInPolicy(this IServiceCollection services, Policy policy)
    {
        services.AddSingleton(policy);
    }
}
