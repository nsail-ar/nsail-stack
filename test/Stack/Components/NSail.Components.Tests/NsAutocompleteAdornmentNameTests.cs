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

/// <summary>The lookup's end adornment is a button — it opens the field's own list — and it was
/// the row kebab's defect in another place: a glyph the Stack configures on a vendor part, with
/// no name on it, so a screen reader met it unnamed and a pointer met it bare (nsail#1865, the
/// Architect's addition to it).
///
/// And a LAZY lookup draws none at all: its list is what a term answers, so the button's one
/// gesture is one it cannot keep — pressed, it focused the box and opened nothing (nsail#2168).
/// The (x) is a different control and the posture keeps it, which is what this suite asks of
/// the vendor rather than of its documentation.
///
/// The hover half of the named button is a CSS bubble and rides in a browser
/// (CollapsedButtonWhisperTests), but the selector that bubble is keyed on is DOM, and it is
/// pinned here: the face is the vendor's icon button inside .mud-autocomplete, and exactly one
/// node in a browsed field is it.</summary>
public sealed class NsAutocompleteAdornmentNameTests : BunitContext, IAsyncLifetime
{
    public NsAutocompleteAdornmentNameTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            ["Common.ShowOptions"] = ShowOptions,
        })]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsAutocompleteAdornmentNameTests).Assembly, []));
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

    const string ShowOptions = "Show options";

    // The selector ns-mud.css keys the adornment's ungated whisper on, written once here so a
    // vendor upgrade that moves the button out of that shape reddens CI rather than taking the
    // bubble away in silence.
    const string Adornment = ".mud-autocomplete .mud-input-adornment-icon-button";

    // The vendor's own clear control, which is not the adornment and does not travel with it.
    const string Clear = ".mud-autocomplete .mud-input-clear-button";

    [Fact]
    public void TheAdornmentOfABrowsedLookupAnswersToAName()
    {
        var cut = Render<SearchLookupHost>();

        var adornment = Assert.Single(cut.FindAll(Adornment));

        Assert.Equal("BUTTON", adornment.NodeName);
        Assert.Equal(ShowOptions, adornment.GetAttribute("aria-label"));
    }

    // A search-first field cannot be browsed, so there is no button offering to browse it: the
    // one that was there focused the box and opened nothing.
    [Fact]
    public void ALazyLookupDrawsNoAdornment()
    {
        var cut = Render<SearchLookupHost>(parameters => parameters.Add(host => host.Lazy, true));

        Assert.Empty(cut.FindAll(Adornment));
    }

    // The (x) rides the same end of the box, so the posture that drew the adornment away has to
    // be asked whether it took this with it: ProductVariantLookup is Lazy AND Clearable, and the
    // graduación a receta proposed is taken off by exactly this control.
    [Fact]
    public void ALazyLookupStillDrawsTheClearItWasGiven()
    {
        var row = new SelectRef(Guid.NewGuid(), "Caja en pesos");

        var cut = Render<SearchLookupHost>(parameters => parameters
            .Add(host => host.Lazy, true)
            .Add(host => host.Clearable, true)
            .Add(host => host.Rows, [row])
            .Add(host => host.Value, row.Id));

        Assert.Single(cut.FindAll(Clear));
    }
}
