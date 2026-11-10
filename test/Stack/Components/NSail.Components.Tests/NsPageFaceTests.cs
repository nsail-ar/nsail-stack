// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Messaging.Runtime.Context;
using NSail.Metadata;

namespace NSail.Components.Tests;

public sealed class NsPageFaceTests : BunitContext
{
    public NsPageFaceTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddComponentServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton<Mediator>(new CapturingMediator());
        Services.AddSingleton<MessageContextAccessor>();
        Services.AddSingleton(new RouteTable(typeof(HomeTestPage).Assembly, []));
    }

    [Fact]
    public void TheFrameCarriesTheCallersIdMessageAndDetail()
    {
        var cut = Render<NsPageFace>(ps => ps
            .Add(p => p.Id, "ns-probe-face")
            .Add(p => p.Message, "Nothing here")
            .Add(p => p.Detail, "trace 42"));

        var frame = cut.Find("#ns-probe-face");

        Assert.Contains("Nothing here", frame.TextContent, StringComparison.Ordinal);
        Assert.Contains("trace 42", frame.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDoorBackHomeIsTheRouteTablesUrlOfTheHomePage()
    {
        var cut = Render<NsPageFace>(ps => ps
            .Add(p => p.Id, "ns-probe-face")
            .Add(p => p.Message, "Nothing here")
            .Add(p => p.HomePage, typeof(HomeTestPage)));

        Assert.Equal("/", cut.Find("#ns-probe-face a").GetAttribute("href"));
    }

    [Fact]
    public void WithoutAHomePageThereIsNoDoor()
    {
        var cut = Render<NsPageFace>(ps => ps
            .Add(p => p.Id, "ns-probe-face")
            .Add(p => p.Message, "Nothing here"));

        Assert.Empty(cut.FindAll("#ns-probe-face a"));
    }

    [Fact]
    public void TheMarkIsDrawnOnlyWhenALogoRouteIsNamed()
    {
        var without = Render<NsPageFace>(ps => ps.Add(p => p.Id, "a").Add(p => p.Message, "m"));
        var with = Render<NsPageFace>(ps => ps.Add(p => p.Id, "b").Add(p => p.Message, "m").Add(p => p.LogoUrl, "/probe/logo.svg"));

        Assert.Empty(without.FindAll(".ns-remote-image"));
        Assert.NotEmpty(with.FindAll(".ns-remote-image"));
    }

    [Fact]
    public void TheCallersActionsRideBesideTheDoor()
    {
        var cut = Render<NsPageFace>(ps => ps
            .Add(p => p.Id, "ns-probe-face")
            .Add(p => p.Message, "m")
            .Add(p => p.HomePage, typeof(HomeTestPage))
            .Add(p => p.Actions, (RenderFragment)(builder => builder.AddMarkupContent(0, "<span id=\"reload\">r</span>"))));

        Assert.NotNull(cut.Find("#ns-probe-face #reload"));
    }
}
