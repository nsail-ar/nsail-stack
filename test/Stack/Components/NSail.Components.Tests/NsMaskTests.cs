// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>Mask in, RAW out. The pattern is NSail's own vocabulary — 0 a digit, everything
/// else a literal — and it is presentation only: what the model holds is the clean characters,
/// because the CUIT check digit is computed over them and every search compares them. A field
/// that stored "20-12345678-9" would break both silently.</summary>
public sealed class NsMaskTests : BunitContext, IAsyncLifetime
{
    // MudBlazor services here are IAsyncDisposable-only, which bUnit's synchronous teardown
    // cannot dispose — the NsActionColumnTests rule.
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    const string TaxId = "00-00000000-0";
    const string DocumentNumber = "00.000.000";

    public NsMaskTests()
    {
        // No MudPopoverProvider in this host: the vendor otherwise refuses to build the popover
        // a tooltip or a picker owns, and none of these assertions is about a popover.
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>())]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <summary>Raw digits arrive from the model and reach the input wearing the pattern.</summary>
    [Fact]
    public void AMaskedField_ShowsTheRawValueInTheShapeOfItsPattern()
    {
        var cut = Render<NsTextField>(ps => ps
            .Add(p => p.Mask, TaxId)
            .Add(p => p.Value, "20123456789"));

        Assert.Equal("20-12345678-9", cut.Find("input").GetAttribute("value"));
    }

    /// <summary>And the DNI shape, the second pattern the kit applies.</summary>
    [Fact]
    public void AMaskedField_FormatsADocumentNumberTheSameWay()
    {
        var cut = Render<NsTextField>(ps => ps
            .Add(p => p.Mask, DocumentNumber)
            .Add(p => p.Value, "12345678"));

        Assert.Equal("12.345.678", cut.Find("input").GetAttribute("value"));
    }

    /// <summary>What comes back from the input carries the literals; what reaches the model
    /// never does.</summary>
    [Fact]
    public async Task AMaskedField_HandsItsOwnerTheRawCharacters()
    {
        string? bound = null;

        var cut = Render<NsTextField>(ps => ps
            .Add(p => p.Mask, TaxId)
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<string?>(this, text => bound = text)));

        await cut.InvokeAsync(() => cut.Find("input").Input("20-12345678-9"));

        Assert.Equal("20123456789", bound);
    }

    /// <summary>A value stored before the mask existed — literals and all — displays right and
    /// is handed back clean, so the first edit normalizes it instead of doubling its
    /// punctuation.</summary>
    [Fact]
    public void AMaskedField_ReadsAValueThatAlreadyCarriesItsLiterals()
    {
        var cut = Render<NsTextField>(ps => ps
            .Add(p => p.Mask, TaxId)
            .Add(p => p.Value, "20-12345678-9"));

        Assert.Equal("20-12345678-9", cut.Find("input").GetAttribute("value"));
    }

    /// <summary>nsail#1991: a CUIT is typed, and the sibling above hands the box its finished
    /// text in one event, which is a paste (testing.md). Every keystroke has to arrive raw on
    /// its own — the check digit is computed off what the model holds, so a number that is only
    /// clean once the box is left refuses the first Guardar under a field visibly holding
    /// it.</summary>
    [Fact]
    public async Task AMaskedField_HandsOverEveryKeystrokeAsItIsTyped()
    {
        var bound = new List<string?>();

        var cut = Render<NsTextField>(ps => ps
            .Add(p => p.Mask, TaxId)
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<string?>(this, text => bound.Add(text))));

        var box = cut.Find("input");

        // The whole box on each event and not the character that arrived, which is what a
        // browser sends: the literals the mask drew are in the text and must not be in the model.
        foreach (var typed in Typing.Prefixes("20-11111111-2"))
        {
            await cut.InvokeAsync(() => box.Input(typed));
        }

        Assert.Equal("20111111112", bound[^1]);
        Assert.DoesNotContain(bound, text => text!.Contains('-', StringComparison.Ordinal));
    }

    /// <summary>No mask, no change: every other text field in the app keeps behaving exactly
    /// as it did.</summary>
    [Fact]
    public async Task AnUnmaskedField_PassesItsValueThroughUntouched()
    {
        string? bound = null;

        var cut = Render<NsTextField>(ps => ps
            .Add(p => p.Value, "20-12345678-9")
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<string?>(this, text => bound = text)));

        Assert.Equal("20-12345678-9", cut.Find("input").GetAttribute("value"));

        await cut.InvokeAsync(() => cut.Find("input").Input("Sarasa S.A."));

        Assert.Equal("Sarasa S.A.", bound);
    }
}
