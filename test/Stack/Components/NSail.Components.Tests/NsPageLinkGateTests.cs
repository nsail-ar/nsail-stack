// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

[Route("/pagelink/open")]
public sealed class OpenPageLinkTestPage : ComponentBase;

[Route("/pagelink/admin")]
[Microsoft.AspNetCore.Authorization.Authorize(Roles = "admin")]
public sealed class AdminPageLinkTestPage : ComponentBase;

/// <summary>Proves NsPageLink asks the same destination gate the nav menu, a lookup's create
/// entry and a link ActionItem already ask (ce3d6110's CanSend/page-gate twin): the rendered
/// anchor is present only for a session the gate allows, and absent -- not just disabled --
/// otherwise, so a portal session never sees a New button it cannot use.</summary>
public sealed class NsPageLinkGateTests : BunitContext
{
    BunitAuthorizationContext SetupFor()
    {
        Services.AddMudServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsPageLinkGateTests).Assembly, []));
        Services.AddScoped<RootSurface>();
        Services.AddScoped<SurfaceHistory>();
        Services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        Services.AddScoped<PageGate>();
        JSInterop.Mode = JSRuntimeMode.Loose;

        return this.AddAuthorization();
    }

    [Fact]
    public void RendersWhenTheSessionMayOpenTheDestination()
    {
        var auth = SetupFor();
        auth.SetAuthorized("someone");

        var cut = Render<NsPageLink<OpenPageLinkTestPage>>(p => p.Add(x => x.Label, "Open"));

        Assert.NotEmpty(cut.FindAll("a"));
    }

    [Fact]
    public void HidesWhenTheSessionMayNotOpenTheDestination()
    {
        var auth = SetupFor();
        auth.SetAuthorized("clerk");
        auth.SetRoles("clerk");

        var cut = Render<NsPageLink<AdminPageLinkTestPage>>(p => p.Add(x => x.Label, "Admin only"));

        Assert.Empty(cut.FindAll("a"));
    }

    [Fact]
    public void RendersWhenTheSessionsRoleMatchesTheDestination()
    {
        var auth = SetupFor();
        auth.SetAuthorized("admin");
        auth.SetRoles("admin");

        var cut = Render<NsPageLink<AdminPageLinkTestPage>>(p => p.Add(x => x.Label, "Admin only"));

        Assert.NotEmpty(cut.FindAll("a"));
    }
}
