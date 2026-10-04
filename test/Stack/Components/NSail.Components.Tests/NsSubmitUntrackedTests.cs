// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>Leonardo, 2026-08-03: Entregar with nothing touched left the hero disabled — the
/// dirty gate NsForm's own Untracked already excuses filters/sign-in from is wrong for a
/// transition surface, which is an act, not an edit. NsSubmit gains the matching Untracked
/// (same word, same idea) so the hero itself can opt out of the gate without un-tracking the
/// whole form — a transition surface still wants its own dirty machinery for the navigation
/// guard and for a returned Problem re-marking the surface (intentional-ui.md).</summary>
public sealed class NsSubmitUntrackedTests : BunitContext, IAsyncLifetime
{
    public NsSubmitUntrackedTests()
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
        return new(typeof(NsSubmitUntrackedTests).Assembly, Array.Empty<System.Reflection.Assembly>());
    }

    [Fact]
    public void OnArrival_WithNothingTouched_TheUntrackedHeroIsEnabled_ThePlainOneIsNot()
    {
        var host = Render<SubmitTrackingHost>(p => p
            .Add(x => x.RouteTable, BuildRouteTable())
            .Add(x => x.Model, new SubmitTrackingModel()));

        Assert.False(host.Instance.Surface!.HasChanges);

        Assert.True(host.Find("button.tracked-submit").HasAttribute("disabled"));
        Assert.False(host.Find("button.untracked-submit").HasAttribute("disabled"));
    }

    [Fact]
    public async Task OnceTheSurfaceIsDirty_ThePlainHeroCatchesUp()
    {
        var host = Render<SubmitTrackingHost>(p => p
            .Add(x => x.RouteTable, BuildRouteTable())
            .Add(x => x.Model, new SubmitTrackingModel()));

        await host.InvokeAsync(() => host.Instance.Surface!.SetDirty(new object()));
        host.Render();

        Assert.False(host.Find("button.tracked-submit").HasAttribute("disabled"));
        Assert.False(host.Find("button.untracked-submit").HasAttribute("disabled"));
    }

    [Fact]
    public async Task SubmittingWithNothingTouched_CompletesTheTransition()
    {
        var submitted = false;

        var host = Render<SubmitTrackingHost>(p => p
            .Add(x => x.RouteTable, BuildRouteTable())
            .Add(x => x.Model, new SubmitTrackingModel())
            .Add(x => x.Submitted, () => submitted = true));

        await host.InvokeAsync(() => host.Find("form").Submit());

        Assert.True(submitted);
    }
}
