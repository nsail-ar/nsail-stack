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

/// <summary>nsail#1905: the declared half of the moment NsFieldRequiredLiftTests states for the
/// field's own. Every text-shaped box commits on the keystroke, and NsDataAnnotationsValidator
/// used to revalidate the changed field on that very event — so a CUIT was refused from its
/// first digit, a mailbox until its @domain closed, and a figure box emptied to be retyped while
/// it stood empty. A refusal belongs to Save, never to the keystroke (intentional-ui.md, Refusal
/// placement); once the form HAS asked, the field tracks its own value again, lifting and
/// returning without a second Save.</summary>
public sealed class NsDeclaredRefusalMomentTests : BunitContext, IAsyncLifetime
{
    public NsDeclaredRefusalMomentTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // MudBlazor's popover provider cannot be torn down by bunit's synchronous Dispose
    // (NsFormRequiredTests), so xunit's async lifecycle takes over.
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    IRenderedComponent<DeclaredRefusalHost> Mount(DeclaredRefusalModel model, Action? submitted = null)
    {
        return Render<DeclaredRefusalHost>(p => p
            .Add(x => x.Model, model)
            .Add(x => x.Submitted, () => submitted?.Invoke()));
    }

    static Task Save(IRenderedComponent<DeclaredRefusalHost> cut)
    {
        return cut.InvokeAsync(() => cut.Find("form").Submit());
    }

    static AngleSharp.Dom.IElement Box(IRenderedComponent<DeclaredRefusalHost> cut, string label)
    {
        return cut.FindAll(".mud-input-control")
            .Single(node => node.QuerySelector("label")?.TextContent == label)
            .QuerySelector("input")!;
    }

    static bool Refuses(IRenderedComponent<DeclaredRefusalHost> cut, string label)
    {
        return cut.FindAll(".mud-input-control")
            .Single(node => node.QuerySelector("label")?.TextContent == label)
            .QuerySelector(".mud-input-error") is not null;
    }

    static string? Refusal(IRenderedComponent<DeclaredRefusalHost> cut, string label)
    {
        return cut.FindAll(".mud-input-control")
            .Single(node => node.QuerySelector("label")?.TextContent == label)
            .QuerySelector(".mud-input-helper-text")
            ?.TextContent;
    }

    // What a browser sends on every keystroke is the whole box, not the character that arrived
    // (testing.md): a single Input of the finished text is a paste, and this story is about the
    // states in between.
    static IEnumerable<string> Prefixes(string text)
    {
        for (var length = 1; length <= text.Length; length++)
        {
            yield return text[..length];
        }
    }

    async Task Type(IRenderedComponent<DeclaredRefusalHost> cut, string label, string text)
    {
        foreach (var typed in Prefixes(text))
        {
            await cut.InvokeAsync(() => Box(cut, label).Input(typed));

            Assert.False(Refuses(cut, label), $"{label} refused at \"{typed}\"");
        }
    }

    /// <summary>Configuración &gt; ARCA: every prefix of a CUIT fails the pattern, so a box
    /// heard on the keystroke drew a refusal from the first digit — and the value still
    /// arrives, which is the half nsail#1937 landed and this one may not undo.</summary>
    [Fact]
    public async Task TypingAPatternedBox_DrawsNoRefusalBeforeTheFirstSave()
    {
        var model = new DeclaredRefusalModel { PointOfSale = 1 };
        var cut = Mount(model);

        await Type(cut, "Cuit", "30123456789");

        Assert.Equal("30123456789", model.Cuit);
        Assert.Empty(cut.FindAll(".mud-input-error"));
    }

    /// <summary>Configuración &gt; Correo &gt; Probar: a mailbox is invalid until its @domain
    /// closes, which is every keystroke but the last few.</summary>
    [Fact]
    public async Task TypingAMailbox_DrawsNoRefusalBeforeTheFirstSave()
    {
        var model = new DeclaredRefusalModel { PointOfSale = 1 };
        var cut = Mount(model);

        await Type(cut, "MailAddress", "alguien@acme.example");

        Assert.Equal("alguien@acme.example", model.MailAddress);
    }

    /// <summary>Punto de venta ARCA: clearing a bounded figure to retype it leaves the box at
    /// the type's own zero, which [Range(1, 99999)] refuses — a refusal under a box the person
    /// is in the middle of answering.</summary>
    [Fact]
    public async Task ClearingAFigureBoxToRetypeIt_DrawsNoRefusalBeforeTheFirstSave()
    {
        var model = new DeclaredRefusalModel { PointOfSale = 3 };
        var cut = Mount(model);

        await cut.InvokeAsync(() => Box(cut, "PointOfSale").Input(string.Empty));

        Assert.False(Refuses(cut, "PointOfSale"));

        await cut.InvokeAsync(() => Box(cut, "PointOfSale").Input("4"));

        Assert.False(Refuses(cut, "PointOfSale"));
        Assert.Equal(4, model.PointOfSale);
    }

    /// <summary>And the gate is the moment and not the rule: Guardar still refuses a half-typed
    /// CUIT, an invalid mailbox, a figure out of range and the empty required name, all at
    /// once.</summary>
    [Fact]
    public async Task TheSaveStillRefusesWhatTheDeclarationRefuses()
    {
        var submitted = false;
        var model = new DeclaredRefusalModel { Cuit = "3012", MailAddress = "alguien@", PointOfSale = 0 };
        var cut = Mount(model, () => submitted = true);

        await Save(cut);

        Assert.False(submitted);
        Assert.True(Refuses(cut, "Name"));
        Assert.True(Refuses(cut, "Cuit"));
        Assert.True(Refuses(cut, "MailAddress"));
        Assert.True(Refuses(cut, "PointOfSale"));
        Assert.True(Refuses(cut, "NamedCuit"));
    }

    /// <summary>The same silence for a box bound by hand and handed its member in For — a
    /// shared editor's shape (PartyIdentityEditor). The vendor control reads that expression's
    /// ValidationAttributes and used to refuse them itself on every value change, in the
    /// attribute's own English, so the house gate alone left those boxes refusing mid-word with
    /// untranslated text; For stops at NsFieldBase now.</summary>
    [Fact]
    public async Task TypingABoxBoundByHand_DrawsNoRefusalBeforeTheFirstSave()
    {
        var model = new DeclaredRefusalModel { PointOfSale = 1 };
        var cut = Mount(model);

        await Type(cut, "NamedCuit", "30123456789");

        Assert.Equal("30123456789", model.NamedCuit);
        Assert.Empty(cut.FindAll(".mud-input-error"));
    }

    /// <summary>And when the Save does refuse that box, the word under it is the house's, from
    /// the catalog — not the vendor's own "The NamedCuit field is required.", which is the
    /// English NsDataAnnotationsValidator exists to keep off a Spanish screen.</summary>
    [Fact]
    public async Task ABoxBoundByHand_IsRefusedInTheHousesWords()
    {
        var cut = Mount(new DeclaredRefusalModel { PointOfSale = 1 });

        await Save(cut);

        Assert.Equal("Problems.Required", Refusal(cut, "NamedCuit"));
    }

    /// <summary>Once the form has asked, the field follows its own value again: the refusal
    /// lifts the keystroke the CUIT completes on, with no second Save — and the field beside it
    /// takes one back the moment it is emptied, which is the other direction of the same
    /// tracking.</summary>
    [Fact]
    public async Task AfterASave_ThePatternedFieldLiftsAsItIsCorrected_AndAnEmptiedOneRefusesAgain()
    {
        var model = new DeclaredRefusalModel { Name = "Ok", Cuit = "3012", PointOfSale = 1 };
        var cut = Mount(model);

        await Save(cut);

        Assert.True(Refuses(cut, "Cuit"));

        await cut.InvokeAsync(() => Box(cut, "Cuit").Input("30123456789"));

        Assert.False(Refuses(cut, "Cuit"));

        await cut.InvokeAsync(() => Box(cut, "Name").Input(string.Empty));

        Assert.True(Refuses(cut, "Name"));
    }

    /// <summary>The same tracking on the figure, where "emptied" is the zero the type falls
    /// back to: a refused form draws it again on the spot.</summary>
    [Fact]
    public async Task AfterASave_AFigureEmptiedAgain_IsRefusedWithoutASecondSave()
    {
        var model = new DeclaredRefusalModel { Name = "Ok", PointOfSale = 0 };
        var cut = Mount(model);

        await Save(cut);

        Assert.True(Refuses(cut, "PointOfSale"));

        await cut.InvokeAsync(() => Box(cut, "PointOfSale").Input("4"));

        Assert.False(Refuses(cut, "PointOfSale"));

        await cut.InvokeAsync(() => Box(cut, "PointOfSale").Input(string.Empty));

        Assert.True(Refuses(cut, "PointOfSale"));
    }

    /// <summary>A second document opening under the same validator asks nothing of its own: the
    /// form rebuilds its EditContext for a new model instance, and a create form opened after a
    /// refused one is as quiet as the first.</summary>
    [Fact]
    public async Task ANewDocumentUnderTheSameForm_DrawsNoRefusalOnTheKeystrokeAgain()
    {
        var cut = Mount(new DeclaredRefusalModel { Name = "Ok", Cuit = "3012", PointOfSale = 1 });

        await Save(cut);

        Assert.True(Refuses(cut, "Cuit"));

        cut.Render(p => p.Add(x => x.Model, new DeclaredRefusalModel { PointOfSale = 1 }));

        await Type(cut, "Cuit", "30123456789");
    }
}
