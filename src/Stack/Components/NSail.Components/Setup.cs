// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace NSail.Components;

public static class Setup
{
    /// <summary>Builds the RouteTable from the registered IRouteContributors, for typed
    /// URLs (GetUrl&lt;TPage&gt;) anywhere in DI — components and services alike — and with it
    /// the default surface those URLs are resolved against.</summary>
    public static void AddRouteTable(this IServiceCollection services)
    {
        // The root surface is the route table plus somewhere to navigate, so it is registered
        // here rather than left to a host to remember: a link that cannot see a surface used
        // to drop its target and hand back the bare route, which navigates correctly and
        // therefore shows nothing. Missing now is a failure to resolve, not a silent one.
        services.TryAddScoped<RootSurface>();

        // Beside the root surface for the same reason, and scoped for one more: the surface
        // that opens an overlay and the one that closes it are different objects, so how the
        // open landed in history is remembered here instead of in either — and an app that
        // has just started remembers nothing, which is what makes a pasted address close by
        // rewriting rather than by walking out of the app.
        services.TryAddScoped<SurfaceHistory>();

        services.TryAddSingleton(provider =>
        {
            var assemblies = provider.GetServices<IRouteContributor>()
                .Select(contributor => contributor.Assembly)
                .Distinct()
                .ToArray();

            return assemblies.Length > 0
                ? new RouteTable(assemblies[0], assemblies[1..])
                : new RouteTable(Assembly.GetEntryAssembly()!, []);
        });
    }

    // NoInlining keeps GetCallingAssembly honest: inlined, it would report this assembly.
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void AddRoutesFromAssembly(this IServiceCollection services, Assembly? assembly = null)
    {
        assembly ??= Assembly.GetCallingAssembly();

        services.AddSingleton<IRouteContributor>(new AssemblyRouteContributor(assembly));
    }

    /// <summary>Teaches a host to answer [Authorize&lt;TMessage&gt;]. Both hosts call it: the
    /// attribute is one string protocol and the page it guards is prerendered on the server
    /// and re-rendered on the client, so a host that cannot read the name kills the page.
    /// Registered without TryAdd so it beats the default provider AddAuthorizationCore
    /// puts in, whichever ran first.</summary>
    public static void AddMessagePolicies(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationPolicyProvider, MessagePolicyProvider>();
        services.AddScoped<IAuthorizationHandler, MessageAuthorizationHandler>();
    }

    public static void AddNavMenu<TContributor>(this IServiceCollection services)
        where TContributor : class, INavMenuContributor
    {
        // The one thing allowed to ask them, registered beside them rather than left to a host
        // to remember — the same reason AddRouteTable registers the root surface. A composition
        // that has contributors at all has something that asks them once for the session.
        services.TryAddScoped<NavMenu>();
        services.AddScoped<INavMenuContributor, TContributor>();
    }

    /// <summary>Registers what says how many things are waiting behind one menu entry. Apart
    /// from AddNavMenu because the two are separate decisions: a module may count for an entry
    /// somebody else planted, and an app that mounted the door may still not want the number on
    /// it.</summary>
    public static void AddNavMenuCount<TCount>(this IServiceCollection services)
        where TCount : class, INavMenuCount
    {
        services.TryAddScoped<NavMenu>();
        services.AddScoped<INavMenuCount, TCount>();
    }

    /// <summary>Registers what this install did to the menu its modules contribute — order,
    /// label and visibility, read from wherever that install keeps it. Apart from AddNavMenu
    /// because mounting and arranging are two verbs: a contributor says what is on the table,
    /// and an arrangement says where what is on the table sits. NavMenu applies every one of
    /// these after every contributor, so no registration order can put a module's word over the
    /// install's.</summary>
    public static void AddNavMenuArrangement<TArrangement>(this IServiceCollection services)
        where TArrangement : class, INavMenuArrangement
    {
        services.TryAddScoped<NavMenu>();
        services.AddScoped<INavMenuArrangement, TArrangement>();
    }

    /// <summary>Registers a dashboard contributor under the app dashboard's contract and
    /// under every IDashboardContributor&lt;TSubject&gt; it implements — one call whichever
    /// dashboard a module contributes to, and one class may contribute to several.</summary>
    public static void AddDashboard<TContributor>(this IServiceCollection services)
        where TContributor : class
    {
        var contracts = typeof(TContributor)
            .GetInterfaces()
            .Where(i => i == typeof(IDashboardContributor)
                || (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDashboardContributor<>)))
            .ToList();

        if (contracts.Count == 0)
        {
            throw new ArgumentException(
                $"{typeof(TContributor).Name} does not implement IDashboardContributor or IDashboardContributor<TSubject>.");
        }

        foreach (var contract in contracts)
        {
            services.AddScoped(contract, typeof(TContributor));
        }
    }

    public static void AddOnboarding<TContributor>(this IServiceCollection services)
        where TContributor : class, IOnboardingContributor
    {
        services.AddScoped<IOnboardingContributor, TContributor>();
    }

    public static void AddGuide<TContributor>(this IServiceCollection services)
        where TContributor : class, IGuideContributor
    {
        // The one thing allowed to fetch a chapter, registered beside the contributors rather
        // than left to a host to remember — the same reason AddNavMenu registers NavMenu.
        services.TryAddScoped<GuideReader>();
        services.AddScoped<IGuideContributor, TContributor>();
    }

    /// <summary>Where this app's guide lives, so every door to it — the guide button on a
    /// page header, a chapter link in the index — resolves an address instead of writing one.
    /// The screen is the product's, so only the product can say it.</summary>
    public static void AddGuidePage<TPage>(this IServiceCollection services)
        where TPage : IComponent
    {
        services.AddSingleton(provider => new GuideRoute(typeof(TPage), provider.GetRequiredService<RouteTable>()));
    }

    /// <summary>Registers a router-level veto, asked for every route before it renders.</summary>
    public static void AddRouteGate<TGate>(this IServiceCollection services)
        where TGate : class, IRouteGate
    {
        services.AddScoped<IRouteGate, TGate>();
    }

    /// <summary>Registers an action contributor under every IActionContributor&lt;TOutlet&gt;
    /// it implements — one class may contribute to several outlets.</summary>
    public static void AddActions<TContributor>(this IServiceCollection services)
        where TContributor : class
    {
        var contracts = typeof(TContributor)
            .GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IActionContributor<>))
            .ToList();

        if (contracts.Count == 0)
        {
            throw new ArgumentException(
                $"{typeof(TContributor).Name} does not implement IActionContributor<TOutlet>.");
        }

        foreach (var contract in contracts)
        {
            services.AddScoped(contract, typeof(TContributor));
        }
    }
}