// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>NsHelp's own mechanism (nsail#182), for a caller whose text is not one fixed
/// translation key — nsail#679-sibling: a work order line's Cantidad falls short of what the
/// store has, and "Solo quedan {0} en stock" is per-row, not a paragraph a Key could name.
/// NsHelp keeps its own single-parameter contract; this is the sibling, so the disclosure
/// mechanics (unloaded until clicked, three ways to dismiss) are proven once and shared rather
/// than re-implemented at the call site.</summary>
public sealed class NsHintTests : BunitContext, IAsyncLifetime
{
    const string Text = "Solo quedan 0 en stock";

    public NsHintTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    IRenderedComponent<NsHintHost> Render()
    {
        return Render<NsHintHost>(p => p.Add(x => x.Text, Text));
    }

    // The trigger's own accessible name carries Text too (it doubles as the desktop hover
    // tooltip), so "not in the document" is read off the popover's own node, not raw markup.
    [Fact]
    public void AtRest_ThePopoverIsNotInTheDocument()
    {
        var cut = Render();

        Assert.Empty(cut.FindAll(".ns-help-text"));
    }

    [Fact]
    public void AtRest_TheButtonCarriesTheTextAsItsOwnAccessibleName()
    {
        var cut = Render();

        var button = cut.Find(".ns-help button");

        // Nothing to key a translation off — the caller's own text names the trigger, the
        // same string a hover would read on a desktop and a click reveals in full on a phone.
        Assert.Equal(Text, button.GetAttribute("aria-label"));
    }

    [Fact]
    public async Task AClick_RendersTheText()
    {
        var cut = Render();

        await cut.InvokeAsync(() => cut.Find(".ns-help button").Click());

        Assert.Contains(Text, cut.Find(".ns-help-text").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Escape_DismissesIt()
    {
        var cut = Render();

        await cut.InvokeAsync(() => cut.Find(".ns-help button").Click());
        Assert.NotEmpty(cut.FindAll(".ns-help-text"));

        await cut.InvokeAsync(() => cut.Find(".ns-help").KeyDown(new KeyboardEventArgs { Key = "Escape" }));

        Assert.Empty(cut.FindAll(".ns-help-text"));
    }

    [Fact]
    public void NsHint_NamesNoVendorTypeInItsApi()
    {
        var vendor = typeof(NsHint)
            .GetProperties()
            .Where(p => p.GetCustomAttributes(typeof(Microsoft.AspNetCore.Components.ParameterAttribute), true).Length != 0)
            .Where(p => p.PropertyType.Namespace?.StartsWith("MudBlazor", StringComparison.Ordinal) == true)
            .ToArray();

        Assert.Empty(vendor);
    }
}
