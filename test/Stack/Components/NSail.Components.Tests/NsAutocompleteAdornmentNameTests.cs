// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

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
/// Architect's addition to it). Both postures, because the glyph differs and the gesture does
/// not: the lupa a lazy lookup draws and the chevron a browsed one does are one button.
///
/// The hover half of it is a CSS bubble and rides in a browser (CollapsedButtonWhisperTests),
/// but the selector that bubble is keyed on is DOM, and it is pinned here: the face is the
/// vendor's icon button inside .mud-autocomplete, and exactly one node in a field is it.</summary>
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheAdornmentAnswersToAName(bool lazy)
    {
        var cut = Render<SearchLookupHost>(parameters => parameters.Add(host => host.Lazy, lazy));

        var adornment = Assert.Single(cut.FindAll(Adornment));

        Assert.Equal("BUTTON", adornment.NodeName);
        Assert.Equal(ShowOptions, adornment.GetAttribute("aria-label"));
    }
}
