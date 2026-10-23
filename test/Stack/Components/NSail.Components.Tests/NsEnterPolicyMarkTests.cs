// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;
using NSail.Testing.Models;

namespace NSail.Components.Tests;

/// <summary>Enter has one policy and it is written in one place — ns.js's Enter listener is the
/// only code that decides whether an Enter keystroke may submit a form (ui/forms.md, Enter): a
/// stray Enter in the middle of a document must not save it, while a form that IS the question
/// keeps the browser's implicit submission. Every read it makes is made from the DOM at the
/// instant the key is pressed, so what can be held at this layer is the MARKS it reads, pinned
/// here so the day a component stops writing one the failure has a name instead of being a sale
/// saved by a keystroke nobody was watching.
///
/// <para>This is NsComboboxExpandedMarkTests' own split, for the same reason: bUnit renders
/// through AngleSharp, which performs no implicit form submission, so neither the prevention nor
/// the grid's move can be proved here. The keystrokes themselves are walked in a browser
/// (Optical's CounterSaleEnterPolicyTests).</para></summary>
public sealed class NsEnterPolicyMarkTests : BunitContext, IAsyncLifetime
{
    public NsEnterPolicyMarkTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
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

    /// <summary>A document being written: the form says it holds one and renders more than one
    /// field, which together are the whole of what the policy asks before taking the key away.</summary>
    [Fact]
    public void ATrackedFormOfSeveralFields_SaysItHoldsADocument()
    {
        var cut = Render<EnterPolicyHost>(p => p.Add(x => x.Fields, 2));

        Assert.Equal("true", cut.Find("form").GetAttribute("data-ns-tracked"));
        Assert.Equal(2, cut.FindAll("[data-ns-field=\"true\"]").Count);
    }

    /// <summary>A form of one field is a question — a dialog collecting an amount, a page's single
    /// box — and keeps the Enter it always had, which is why the count and not the tracking alone
    /// is what the policy reads.</summary>
    [Fact]
    public void AFormOfOneField_RendersOneFieldToCount()
    {
        var cut = Render<EnterPolicyHost>(p => p.Add(x => x.Fields, 1));

        Assert.Equal("true", cut.Find("form").GetAttribute("data-ns-tracked"));
        Assert.Single(cut.FindAll("[data-ns-field=\"true\"]"));
    }

    /// <summary>A filter bar, a search form, a sign-in: Untracked is the form saying it holds no
    /// document, and the word reaches the keystroke as the form's own attribute.</summary>
    [Fact]
    public void AnUntrackedForm_SaysItHoldsNone()
    {
        var cut = Render<EnterPolicyHost>(p => p.Add(x => x.Untracked, true));

        Assert.Equal("false", cut.Find("form").GetAttribute("data-ns-tracked"));
    }

    /// <summary>The count is of FIELDS and not of text inputs: an area whose Enter is text and a
    /// select whose value arrives from a pick are both fields of the document, and a form of two
    /// of them is a document too.</summary>
    [Fact]
    public void EveryShapeOfBox_AnnouncesItselfAsAField()
    {
        var cut = Render<EnterPolicyHost>(p => p
            .Add(x => x.Fields, 1)
            .Add(x => x.Boxes, true));

        Assert.Equal(3, cut.FindAll("[data-ns-field=\"true\"]").Count);
        Assert.Equal("true", cut.Find("textarea").GetAttribute("data-ns-field"));
    }

    /// <summary>A grid whose rows are typed into says so, and carries the three controls the key
    /// presses to confirm a row and go down the column: there is no second path a row is
    /// committed by, so the key and the button cannot come apart.</summary>
    [Fact]
    public async Task AGridOfferingAWayIntoItsRows_CarriesTheRowsControls()
    {
        var cut = Render<RowEditorHost>();

        Assert.NotNull(cut.Find(".ns-cells"));
        Assert.NotNull(cut.Find(".ns-cells-add"));
        Assert.NotNull(cut.Find(".ns-row-edit"));

        await cut.InvokeAsync(() => cut.FindComponent<NsTable<TestModel>>().Instance.Add());

        Assert.NotNull(cut.Find(".ns-row-commit"));
    }

    /// <summary>A page that adds from chrome of its own takes the control off the bar and the
    /// grid keeps it undrawn: the way in the keystroke presses is always a control of the
    /// GRID's, so the key runs the code the click runs and the page's own Agregar is never
    /// pressed from here. Clipped and not removed — out of the layout is how the keystroke reads
    /// "there is no way in" — and out of the accessibility tree and the tab order, which leaves
    /// the one Agregar a hand can reach the one the page drew.</summary>
    [Fact]
    public async Task AGridWhosePageDrawsTheAdd_KeepsTheWayInUndrawn()
    {
        var cut = Render<RowEditorHost>(p => p
            .Add(x => x.Titled, false)
            .Add(x => x.AddControl, false));

        var add = Assert.Single(cut.FindAll(".ns-cells-add"));

        Assert.Contains("ns-cells-add-undrawn", add.ClassName);
        Assert.Equal("true", add.GetAttribute("aria-hidden"));
        Assert.Equal("-1", add.GetAttribute("tabindex"));

        await cut.InvokeAsync(() => add.Click());

        Assert.NotNull(cut.Find(".ns-row-commit"));
    }

    /// <summary>A grid that names itself draws the one control in its bar and keeps no second
    /// one: the undrawn way in is the other half of the same single seat.</summary>
    [Fact]
    public void AGridThatNamesItself_DrawsTheOnlyWayIn()
    {
        var cut = Render<RowEditorHost>();

        var add = Assert.Single(cut.FindAll(".ns-cells-add"));

        Assert.DoesNotContain("ns-cells-add-undrawn", add.ClassName);
    }

    /// <summary>The other row host answers the same way where its page names the collection: a
    /// null Title draws no bar at all, and the list keeps the keystroke's way in undrawn.</summary>
    [Fact]
    public async Task AListEditorWhosePageNamesIt_KeepsTheWayInUndrawn()
    {
        var cut = Render<ListEditorFormHost>(p => p.Add(x => x.Titled, false));

        var add = Assert.Single(cut.FindAll(".ns-cells-add"));

        Assert.Contains("ns-cells-add-undrawn", add.ClassName);
        Assert.Equal("true", add.GetAttribute("aria-hidden"));
        Assert.Equal("-1", add.GetAttribute("tabindex"));

        await cut.InvokeAsync(() => add.Click());

        Assert.NotNull(cut.Find("ul.ns-cells"));
        Assert.NotNull(cut.Find(".ns-row-commit"));
    }

    /// <summary>A row's own controls are not fields, and that is what keeps the policy off them:
    /// both reads reach for a box being typed into, so the pencil, the Confirmar and the
    /// Cancelar standing inside the very rows and the very form the policy answers for keep the
    /// activation Enter gives a focused button.</summary>
    [Fact]
    public async Task ARowsOwnControls_AreNotFields()
    {
        var cut = Render<RowEditorHost>();

        Assert.Null(cut.Find(".ns-row-edit").GetAttribute("data-ns-field"));

        await cut.InvokeAsync(() => cut.FindComponent<NsTable<TestModel>>().Instance.Add());

        Assert.Null(cut.Find(".ns-row-commit").GetAttribute("data-ns-field"));
    }

    /// <summary>A grid nobody types into is READ, so its Enter is the document's and not the
    /// row's — the mark is the way in, never the table.</summary>
    [Fact]
    public void AGridWithNoWayIntoARow_IsNotMarked()
    {
        var cut = Render<RowClickTableHost>();

        Assert.Empty(cut.FindAll(".ns-cells"));
    }

    /// <summary>The other row host answers the same way with the same marks, so the policy names
    /// neither component and the next host writes the marks rather than a second rule.</summary>
    [Fact]
    public async Task AListEditorsRows_CarryTheSameMarks()
    {
        var cut = Render<ListEditorFormHost>();

        Assert.NotNull(cut.Find(".ns-cells-add"));

        await cut.InvokeAsync(() => cut.Find(".ns-cells-add").Click());

        Assert.NotNull(cut.Find("ul.ns-cells"));
        Assert.NotNull(cut.Find(".ns-row-commit"));
    }

    /// <summary>A COMPOSER is the one textarea whose Enter is not a line: there the words being
    /// typed ARE the message, so the policy sends instead of standing down. Both marks it reads
    /// are pinned here — the composer around the box, which it finds from the keystroke's own
    /// target upwards, and the send face inside it, which is what the key presses
    /// (NsSendMessageTests holds what that face does).</summary>
    [Fact]
    public void AComposer_CarriesTheMarkAndTheFaceTheKeyPresses()
    {
        var cut = Render<SendMessageHost>();

        var composer = cut.Find("textarea").Closest(".ns-send");

        Assert.NotNull(composer);
        Assert.NotNull(composer.QuerySelector("button[type=\"submit\"]"));
    }
}
