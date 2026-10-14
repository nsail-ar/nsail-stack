// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#2207: "Saltar al contenido" is the first stop on every page, so a person
/// arriving by keyboard reaches the screen they opened instead of walking the whole app map
/// first. First is a fact about DOCUMENT ORDER and nothing else — it is the only order Tab
/// reads — which is why it is pinned here rather than left to the eye: the link is invisible
/// until it has focus, so nothing about it is noticed when it stops being first.
/// <para>Whether it is off screen while unfocused is the stylesheet's half (ns-mud.css, THE
/// SKIP LINK) and no bUnit assertion reaches it.</para></summary>
public sealed class SkipLinkTests : BunitContext, IAsyncLifetime
{
    public SkipLinkTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
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

    [Fact]
    public void TheSkipLink_IsTheFirstTabStopOfTheShell()
    {
        var host = Render<LayoutGutterHost>();

        var stops = host.FindAll("a[href], button, input, select, textarea");

        Assert.NotEmpty(stops);
        Assert.Contains("ns-skip", stops[0].GetAttribute("class"), StringComparison.Ordinal);
    }

    // The link's address and the content's id are one constant (NsMainLayout.ContentId), so a
    // door onto a room that is not there cannot be rendered.
    [Fact]
    public void TheSkipLink_NamesTheContentItLandsOn()
    {
        var host = Render<LayoutGutterHost>();

        var target = host.Find("a.ns-skip").GetAttribute("href")?.TrimStart('#');

        Assert.False(string.IsNullOrEmpty(target));
        Assert.NotNull(host.Find($"#{target}"));
    }

    // A fragment moves the cursor only where its target can hold one: without this the link
    // scrolls and leaves Tab exactly where it was.
    [Fact]
    public void TheContent_CanHoldTheCursorItIsHanded()
    {
        var host = Render<LayoutGutterHost>();

        var target = host.Find("a.ns-skip").GetAttribute("href")?.TrimStart('#');

        Assert.Equal("-1", host.Find($"#{target}").GetAttribute("tabindex"));
    }
}
