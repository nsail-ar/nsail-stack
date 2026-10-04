// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#1267: a line that is always on screen and the detail behind it. Closed is the
/// state it is born in — the ruling the control exists for — and the detail stays in the
/// document while it is closed, because what the summary states is derived from what the detail
/// holds (Nueva Venta's footer reads its total off the block of figures the chevron opens).</summary>
public sealed class NsExpanderTests : BunitContext, IAsyncLifetime
{
    public NsExpanderTests()
    {
        Services.AddMudServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void ItIsBornClosed_WithItsDetailStillInTheDocument()
    {
        var cut = Render<ExpanderHost>();

        Assert.DoesNotContain("ns-expander-open", cut.Find(".ns-expander").GetAttribute("class"), StringComparison.Ordinal);
        Assert.Equal("false", Chevron(cut).GetAttribute("aria-expanded"));

        // In the document and hidden by the stylesheet, never absent: a total derived from
        // figures that are not rendered is a total nobody can draw.
        Assert.NotNull(cut.Find(".ns-expander-detail").QuerySelector(".working"));
    }

    [Fact]
    public async Task TheChevron_OpensTheDetailAndClosesItAgain()
    {
        var cut = Render<ExpanderHost>();

        await cut.InvokeAsync(() => Chevron(cut).Click());

        Assert.Contains("ns-expander-open", cut.Find(".ns-expander").GetAttribute("class"), StringComparison.Ordinal);
        Assert.Equal("true", Chevron(cut).GetAttribute("aria-expanded"));

        await cut.InvokeAsync(() => Chevron(cut).Click());

        Assert.DoesNotContain("ns-expander-open", cut.Find(".ns-expander").GetAttribute("class"), StringComparison.Ordinal);
        Assert.Equal("false", Chevron(cut).GetAttribute("aria-expanded"));
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    // The chevron's tooltip is a popover, and MudBlazor's popover service is IAsyncDisposable
    // only: bUnit's synchronous teardown throws on it, so this context is torn down async.
    public new async Task DisposeAsync()
    {
        await ((IAsyncDisposable)this).DisposeAsync();
    }

    static AngleSharp.Dom.IElement Chevron(IRenderedComponent<ExpanderHost> cut)
    {
        return cut.Find(".ns-expander-toggle");
    }
}
