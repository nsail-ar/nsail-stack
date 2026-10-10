// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;
using Xunit;

namespace NSail.Components.Tests;

/// <summary>A Lazy lookup draws no adornment (NsAutocompleteAdornmentNameTests), so the word in
/// the box is the only thing left saying the field is searched rather than browsed — and the
/// posture composes that word instead of asking every lookup to carry a search string: the item
/// type's concept through Common.SearchPrompt, the same prompt the fallback label is composed
/// from. Pinned here because the rule and the field are two repositories apart: a product's
/// pickers pass no Placeholder at all, so the box reading its concept is the Stack's own answer
/// or nobody's.</summary>
public sealed class NsLookupPromptTests : BunitContext, IAsyncLifetime
{
    public NsLookupPromptTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(_strings)]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsLookupPromptTests).Assembly, []));
        Services.AddScoped<RootSurface>();
        Services.AddScoped<SurfaceHistory>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // MudPopoverProvider resolves a MudBlazor service that is IAsyncDisposable-only, so bUnit's
    // synchronous teardown cannot dispose it (NsLookupCreateEntryTests' own note).
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    // The fixture's item type reached the way the field reaches it, so a metadata template that
    // moved would redden here rather than take the prompt away in silence.
    const string Concept = "Components.SelectRef";

    const string Prompt = "Buscar {0}";

    // Held as the live dictionary the catalog reads, because one fact below is about a concept
    // the catalog does NOT carry and the services are arranged once, in the constructor.
    readonly Dictionary<string, string> _strings = new()
    {
        ["Common.SearchPrompt"] = Prompt,
        [Concept] = "Cuentas",
    };

    // The second assertion is what makes the word READ at rest rather than only under focus: the
    // vendor floats the label off .mud-shrink on the input (its CSS is `.mud-shrink ~ label`),
    // and it puts that class on for a placeholder — so an empty, unfocused box shows the prompt
    // with the label above it instead of the label sitting on the prompt.
    [Fact]
    public void ALazyLookupSaysInTheBoxWhatItSearches()
    {
        var cut = Render<SearchLookupHost>(parameters => parameters.Add(host => host.Lazy, true));

        Assert.Equal("Buscar Cuentas", Placeholder(cut));
        Assert.Contains("mud-shrink", cut.Find("div.mud-input").GetAttribute("class"));
    }

    // The browsed posture keeps the chevron, which says the same thing in the same place a
    // glyph can say it; a second word in the box would be the field talking twice.
    [Fact]
    public void ABrowsedLookupSaysNothingInTheBox()
    {
        var cut = Render<SearchLookupHost>();

        Assert.True(string.IsNullOrEmpty(Placeholder(cut)));
    }

    // A field whose own word is better than the concept's — a box that is scanned into, a term
    // the screen names — writes it, and the composition never overrides what it was given.
    [Fact]
    public void APlaceholderTheCallerWroteWins()
    {
        var cut = Render<SearchLookupHost>(parameters => parameters
            .Add(host => host.Lazy, true)
            .Add(host => host.Placeholder, "Escaneá el código"));

        Assert.Equal("Escaneá el código", Placeholder(cut));
    }

    // An untranslated key is not a prompt: a concept the catalog does not name leaves the box
    // empty rather than printing "Components.SelectRef" at the operator.
    [Fact]
    public void AConceptWithNoTranslationComposesNothing()
    {
        _strings.Remove(Concept);

        var cut = Render<SearchLookupHost>(parameters => parameters.Add(host => host.Lazy, true));

        Assert.True(string.IsNullOrEmpty(Placeholder(cut)));
    }

    static string? Placeholder(IRenderedComponent<SearchLookupHost> cut)
    {
        return cut.Find("input.mud-input-root").GetAttribute("placeholder");
    }
}
