// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>The default surface outlives navigation — only named surfaces remount per route —
/// so every dirty source a page leaves on it is inherited by the next page. A form already
/// clears its own on unmount; a component that reports itself (a collection editor) must too,
/// or one address added and one "leave anyway" makes the surface permanently dirty: every
/// later screen offers an enabled submit and asks about changes nobody made.</summary>
public sealed class SurfaceDirtyLifetimeTests : BunitContext, IAsyncLifetime
{
    public SurfaceDirtyLifetimeTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddComponentServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
    }

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    static RouteTable BuildRouteTable()
    {
        return new(typeof(SurfaceDirtyLifetimeTests).Assembly, Array.Empty<System.Reflection.Assembly>());
    }

    IRenderedComponent<SurfaceLifetimeHost> RenderHost()
    {
        return Render<SurfaceLifetimeHost>(p => p.Add(x => x.RouteTable, BuildRouteTable()));
    }

    [Fact]
    public async Task AReportingComponentThatUnmounts_TakesItsDirtyWithIt()
    {
        var host = RenderHost();

        await host.InvokeAsync(() => host.Instance.Probe!.MarkDirty());

        Assert.True(host.Instance.Surface!.HasChanges);

        host.Render(p => p.Add(x => x.Showing, false));

        Assert.False(host.Instance.Surface!.HasChanges);
    }

    [Fact]
    public async Task TheNextPagesSubmit_IsNotEnabledByTheLastPagesDirt()
    {
        var host = RenderHost();

        await host.InvokeAsync(() => host.Instance.Probe!.MarkDirty());

        host.Render(p => p.Add(x => x.Showing, false));

        Assert.True(host.Find("button.next-submit").HasAttribute("disabled"));
    }

    /// <summary>The other half of the unmount, and the half a spend cannot cover: a gesture still
    /// in flight reports itself when it lands, after its component is gone. OpticalJobCard is the
    /// shape — it reprices and refits, two reads, before it tells the surface — so a Crear that
    /// navigates away mid-read files the report on a surface with no page left to spend it, and
    /// leaving the next screen asks about changes nobody made. Dispose has to REFUSE what comes
    /// after it, not merely spend what came before.</summary>
    [Fact]
    public async Task AReportThatLandsAfterItsComponentUnmounted_IsRefused()
    {
        var host = RenderHost();
        var probe = host.Instance.Probe!;

        host.Render(p => p.Add(x => x.Showing, false));

        await host.InvokeAsync(probe.MarkDirty);

        Assert.False(host.Instance.Surface!.HasChanges);
        Assert.True(host.Find("button.next-submit").HasAttribute("disabled"));
    }

    /// <summary>The form half of the same rule, already true and pinned here beside it.</summary>
    [Fact]
    public async Task AFormThatUnmounts_TakesItsDirtyWithIt()
    {
        var host = RenderHost();

        await host.InvokeAsync(() => host.Find("input[type=text]").Input("typed by hand"));

        Assert.True(host.Instance.Surface!.HasChanges);

        host.Render(p => p.Add(x => x.Showing, false));

        Assert.False(host.Instance.Surface!.HasChanges);
    }
}
