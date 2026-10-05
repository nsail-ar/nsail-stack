// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>A table promises homogeneous columns; a list of heterogeneous items promises
/// nothing of the kind — each item shows only the fields it has, on a flex row that folds onto
/// a second line instead of strangling a column half the items would leave empty. The chrome
/// (the title bar with its add control, the quiet foot) is built in rather than wrapped around
/// by each consumer, and the lifecycle is the RowEditor's own: edit, commit, cancel, remove,
/// with a refusal keeping the item open where it can still be fixed.</summary>
public sealed class NsListEditorTests : BunitContext, IAsyncLifetime
{
    public NsListEditorTests()
    {
        Services.AddMudServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            ["Actions.Add"] = "Agregar",
            ["Actions.Edit"] = "Editar",
            ["Actions.Delete"] = "Eliminar",
            ["Actions.Confirm"] = "Confirmar",
            ["Actions.Cancel"] = "Cancelar",
            ["Common.NoRecords"] = "Sin registros",
            ["Problems.Required"] = "Obligatorio",
        })]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // MudPopoverProvider resolves a MudBlazor service that is internal and IAsyncDisposable
    // only, so bUnit's synchronous teardown cannot dispose it (NsSelectTests' own note).
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    static Task Click(IRenderedComponent<ListEditorHost> cut, string name)
    {
        return cut.InvokeAsync(() => cut.FindAll($"button[aria-label='{name}']")[0].Click());
    }

    [Fact]
    public void TheChromeIsBuiltIn()
    {
        var cut = Render<ListEditorHost>();

        var bar = cut.Find(".ns-collection-bar");

        Assert.Contains("Pagos", bar.TextContent);
        Assert.NotNull(bar.QuerySelector("button[aria-label='Agregar']"));
        Assert.Equal("3", cut.Find(".foot").TextContent);
        Assert.Single(cut.FindAll("li.ns-list-editor-item"));

        // The bar and the totals share the family's own horizontal inset (ns-mud.css,
        // "Sales: el 'sin margenes' se modera") rather than sitting flush against the card's
        // border — Footer's own markup carries none of it, so the wrapper is what earns it.
        Assert.Contains("ns-collection-foot", cut.Find(".foot").ParentElement!.ClassList);
    }

    /// <summary>The structural win over a table, and the reason the component exists: an item is
    /// one flex row that WRAPS, inside a real list a screen reader can announce as one.</summary>
    [Fact]
    public void AnItemIsAFlexRowThatWraps()
    {
        var cut = Render<ListEditorHost>();

        var item = cut.Find("ul.ns-list-editor > li.ns-list-editor-item");

        Assert.Contains("d-flex", item.ClassList);
        Assert.Contains("flex-wrap", item.ClassList);

        // The item's own content is a wrapping row too, so the fields fold rather than the
        // affordances being pushed off the line.
        Assert.Contains("flex-wrap", item.QuerySelector("div")!.ClassList);
    }

    [Fact]
    public void ReadOnlyLeavesNothingToPressOn()
    {
        var cut = Render<ListEditorHost>(ps => ps.Add(p => p.ReadOnly, true));

        Assert.Empty(cut.FindAll("button[aria-label='Agregar']"));
        Assert.Empty(cut.FindAll("button[aria-label='Editar']"));
        Assert.Empty(cut.FindAll("button[aria-label='Eliminar']"));
        Assert.Contains("Ana", cut.Find(".item-name").TextContent);
    }

    /// <summary>NewItem mints the item and nothing else — the list is what appends it to the
    /// caller's collection and opens it. An editor renders only for a row the list iterates, so
    /// a row minted outside Items would grey the add control over nothing (nsail#817).</summary>
    [Fact]
    public async Task AddingAppendsWhatNewItemMintedAndOpensItsEditor()
    {
        var cut = Render<ListEditorHost>();

        await Click(cut, "Agregar");

        Assert.Equal(2, cut.Instance.Rows.Count);
        Assert.Equal("Nuevo", cut.Instance.Rows[1].Name);

        var editing = cut.FindAll("li.ns-list-editor-item")[1];

        Assert.NotEmpty(editing.QuerySelectorAll("input"));
        Assert.NotNull(editing.QuerySelector("button[aria-label='Confirmar']"));
        Assert.NotNull(editing.QuerySelector("button[aria-label='Cancelar']"));
    }

    /// <summary>The add works with nothing wired past the two templates: what the list adds is
    /// not the host's to add, so a host cannot leave it out.</summary>
    [Fact]
    public async Task AddingWorksWithNoHandlersWired()
    {
        var cut = Render<ListEditorHost>(ps => ps.Add(p => p.Unwired, true));

        await Click(cut, "Agregar");

        Assert.Equal(2, cut.Instance.Rows.Count);
        Assert.NotEmpty(cut.FindAll("li.ns-list-editor-item")[1].QuerySelectorAll("input"));

        // No OnRemove means no delete control, which is the one affordance that IS the host's.
        Assert.Empty(cut.FindAll("button[aria-label='Eliminar']"));
    }

    /// <summary>And backing out of it works too: the row the list put in is the list's to take
    /// back, so a host with no OnRemove is left with no phantom half-typed row.</summary>
    [Fact]
    public async Task CancellingAFreshItemTakesItBackOutWithNoHandlersWired()
    {
        var cut = Render<ListEditorHost>(ps => ps.Add(p => p.Unwired, true));

        await Click(cut, "Agregar");
        await Click(cut, "Cancelar");

        Assert.Single(cut.Instance.Rows);
        Assert.Empty(cut.FindAll("li.ns-list-editor-item input"));
    }

    [Fact]
    public async Task ConfirmingClosesTheEditorBackToTheSentence()
    {
        var cut = Render<ListEditorHost>();

        await Click(cut, "Agregar");
        await Click(cut, "Confirmar");

        Assert.Equal(1, cut.Instance.Commits);
        Assert.Empty(cut.FindAll("li.ns-list-editor-item input"));
        Assert.Equal(2, cut.FindAll(".item-name").Count);
    }

    /// <summary>A refusal travels on the args, never as a throw, and it keeps the item open —
    /// collapsing back to the display sentence would take the half-typed values away from the
    /// person who has to correct them.</summary>
    [Fact]
    public async Task ARefusedCommitKeepsTheItemOpen()
    {
        var cut = Render<ListEditorHost>(ps => ps.Add(p => p.Refuse, true));

        await Click(cut, "Agregar");
        await Click(cut, "Confirmar");

        Assert.Equal(1, cut.Instance.Commits);
        Assert.NotEmpty(cut.FindAll("li.ns-list-editor-item input"));
        Assert.NotEmpty(cut.FindAll("button[aria-label='Confirmar']"));
    }

    /// <summary>An item that was never confirmed exists only because the user asked for one, so
    /// backing out of it takes it back out of the collection.</summary>
    [Fact]
    public async Task CancellingAFreshItemTakesItBackOut()
    {
        var cut = Render<ListEditorHost>();

        await Click(cut, "Agregar");
        await Click(cut, "Cancelar");

        Assert.Single(cut.Instance.Rows);
        Assert.Empty(cut.FindAll("li.ns-list-editor-item input"));
    }

    /// <summary>Every affordance is an icon button, and a tooltip paints a popover rather than
    /// naming what it hovers — so each one carries the Actions string as its accessible name,
    /// exactly as the RowEditor's do.</summary>
    [Fact]
    public async Task EveryAffordanceCarriesItsName()
    {
        var cut = Render<ListEditorHost>();

        var names = cut.FindAll("li.ns-list-editor-item button")
            .Select(button => button.GetAttribute("aria-label"))
            .ToList();

        Assert.Contains("Editar", names);
        Assert.Contains("Eliminar", names);

        await Click(cut, "Editar");

        var editing = cut.FindAll("li.ns-list-editor-item button")
            .Select(button => button.GetAttribute("aria-label"))
            .ToList();

        Assert.Contains("Confirmar", editing);
        Assert.Contains("Cancelar", editing);
    }

    /// <summary>While an item is open every other item's controls are inert: there is no
    /// half-typed item to lose track of and no order to resolve between two.</summary>
    [Fact]
    public async Task OnlyOneItemIsOpenAtATime()
    {
        var cut = Render<ListEditorHost>();

        await Click(cut, "Editar");

        Assert.True(cut.Find("button[aria-label='Agregar']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task AnEmptyCollectionSaysSo()
    {
        var cut = Render<ListEditorHost>();

        await Click(cut, "Eliminar");

        Assert.Empty(cut.FindAll("li.ns-list-editor-item"));
        Assert.Contains("Sin registros", cut.Markup);

        // The empty message stands in for a row (the Seña de Encargar OT read exactly this
        // way, "No hay registros" flush against the card) and shares the same inset.
        Assert.Contains("Sin registros", cut.Find(".ns-list-editor-empty").TextContent);
    }

    /// <summary>ChannelsEditor's shape: no ItemEditor, no NewItem, so the bar's + comes from
    /// OnAdd instead — and invoking it never opens the built-in row editor, because there is
    /// none to open.</summary>
    [Fact]
    public async Task OnAddMode_TheBarAddInvokesOnAdd_AndOpensNoInPlaceEditor()
    {
        var cut = Render<ListEditorHost>(ps => ps.Add(p => p.OnAddMode, true));

        await Click(cut, "Agregar");

        Assert.Equal(1, cut.Instance.OnAddInvocations);
        Assert.Equal(2, cut.Instance.Rows.Count);
        Assert.Contains("ViaDialog", cut.Instance.Rows[1].Name);
        Assert.Empty(cut.FindAll("li.ns-list-editor-item input"));
    }

    [Fact]
    public void OnAddMode_ReadOnlyLeavesNoAddButton()
    {
        var cut = Render<ListEditorHost>(ps => ps
            .Add(p => p.OnAddMode, true)
            .Add(p => p.ReadOnly, true));

        Assert.Empty(cut.FindAll("button[aria-label='Agregar']"));
    }

    /// <summary>A collection with hand entry cannot express a SECOND way in as the bar's +:
    /// OnAdd is exclusive with in-place editing by construction (ShowOnAdd), so a pull that
    /// fills the list from somewhere else rides Actions instead. Add keeps the edge — nothing a
    /// caller already wired moves — and the contributed act sits before it.</summary>
    [Fact]
    public async Task ContributedActsRideTheBarAndAddKeepsTheEdge()
    {
        var cut = Render<ListEditorHost>(ps => ps.Add(p => p.Contributed, true));

        var bar = cut.Find(".ns-collection-bar");
        var buttons = bar.QuerySelectorAll("button");

        Assert.Equal(2, buttons.Length);
        Assert.Contains("Traer", buttons[0].TextContent, StringComparison.Ordinal);
        Assert.Equal("Agregar", buttons[1].GetAttribute("aria-label"));

        await cut.InvokeAsync(() => cut.Find(".ns-collection-bar button").Click());

        Assert.Equal(1, cut.Instance.BringInvocations);
        Assert.Equal(2, cut.Instance.Rows.Count);

        // The pull is not the row editor: nothing opened in place.
        Assert.Empty(cut.FindAll("li.ns-list-editor-item input"));
    }

    /// <summary>Everything Actions carries is a way IN, so ReadOnly takes it with the + rather
    /// than leaving each caller to remember: a read-only list has no way in at all.</summary>
    [Fact]
    public void ReadOnlyTakesTheContributedActsWithTheAddControl()
    {
        var cut = Render<ListEditorHost>(ps => ps
            .Add(p => p.Contributed, true)
            .Add(p => p.ReadOnly, true));

        Assert.Empty(cut.Find(".ns-collection-bar").QuerySelectorAll("button"));
        Assert.Contains("Ana", cut.Find(".item-name").TextContent);
    }
    // Asked of the input itself rather than counted in the markup: MudBlazor stamps
    // .mud-input-error on several nested nodes of one errored field, so a count of them says
    // nothing about which field was marked (NsFormProblemDisplayTests' own note).
    static string? NameError(IRenderedComponent<ListEditorHost> cut)
    {
        var field = cut.FindComponents<MudTextField<string>>().Single();

        return field.Instance.Error ? field.Instance.ErrorText : null;
    }

    /// <summary>The claim: a red "Obligatorio" landed in the corner and the Nombre field carried
    /// no mark, so the person had to connect a message in a corner back to the row they had just
    /// confirmed. The refusal names a member the open row renders a field for, so it is drawn
    /// under that field — and the list says it drew it (args.Handled), which is what keeps it off
    /// the snackbar the surface would otherwise raise.</summary>
    [Fact]
    public async Task ARefusalNamingAFieldOfTheRowDrawsUnderThatField()
    {
        var cut = Render<ListEditorHost>(ps => ps.Add(p => p.Refuse, true));

        await Click(cut, "Agregar");
        await Click(cut, "Confirmar");

        Assert.Equal("Obligatorio", NameError(cut));
        Assert.Empty(cut.FindAll(".ns-form-problem"));
        Assert.Equal([true], cut.Instance.Reports);
    }

    /// <summary>A "no" the row renders no control for — a rule over the whole item — has nothing
    /// to anchor to, so it draws ONCE at the foot of the open row, in the sender's own words,
    /// and still never as a toast.</summary>
    [Fact]
    public async Task ARefusalNamingNoFieldOfTheRowDrawsOnceAtTheFootOfTheOpenRow()
    {
        var cut = Render<ListEditorHost>(ps => ps.Add(p => p.RefuseRule, true));

        await Click(cut, "Agregar");
        await Click(cut, "Confirmar");

        var strip = Assert.Single(cut.FindAll("li.ns-list-editor-item .ns-list-editor-problem"));

        Assert.Equal("Ya hay un pago de ese tipo", strip.TextContent.Trim());
        Assert.Equal("alert", strip.GetAttribute("role"));
        Assert.Null(NameError(cut));
        Assert.Equal([true], cut.Instance.Reports);
    }

    /// <summary>The refused row is still the row being edited, so the answer lifts the moment it
    /// is answered: a second Confirmar that takes closes the editor with nothing left standing
    /// anywhere — neither under the field nor at the row's foot.</summary>
    [Fact]
    public async Task AnsweringTheRefusalClosesTheRowWithNothingLeftStanding()
    {
        var cut = Render<ListEditorHost>(ps => ps.Add(p => p.Refuse, true));

        await Click(cut, "Agregar");
        await Click(cut, "Confirmar");

        Assert.Equal("Obligatorio", NameError(cut));

        cut.Render(ps => ps.Add(p => p.Refuse, false));

        await Click(cut, "Confirmar");

        Assert.Empty(cut.FindAll("li.ns-list-editor-item input"));
        Assert.Empty(cut.FindAll(".mud-input-helper-text"));
        Assert.Empty(cut.FindAll(".ns-form-problem"));
    }

    /// <summary>The open row is the only one that reads actions-last once its fields have
    /// stacked, so it is the only one that says so in its class: a folded row keeps its trailing
    /// affordances at every width (ns-mud.css, .ns-list-editor-open).</summary>
    [Fact]
    public async Task OnlyTheOpenRowCarriesTheStackedEditorClass()
    {
        var cut = Render<ListEditorHost>();

        Assert.Empty(cut.FindAll("li.ns-list-editor-open"));

        await Click(cut, "Editar");

        Assert.Single(cut.FindAll("li.ns-list-editor-open"));
        Assert.Single(cut.FindAll("li.ns-list-editor-open .ns-list-editor-acts"));
    }

    /// <summary>Read the class, because the class is the whole mechanism: MudBlazor's
    /// `align-center` is `align-items: center !important`, so an open row wearing it would pin
    /// its alignment past any query — the stacked row's fields and its refusal strip would be
    /// centred and shrink-to-fit instead of taking the row's width. bUnit lays out no box, so
    /// what can be pinned here is the one thing the cascade turns on: the open row does not wear
    /// the utility and a folded row still does (ns-mud.css, .ns-list-editor-open).</summary>
    [Fact]
    public async Task TheOpenRowWearsNoVendorAlignmentUtility()
    {
        var cut = Render<ListEditorHost>();

        Assert.Single(cut.FindAll("li.ns-list-editor-item.align-center"));

        await Click(cut, "Editar");

        Assert.Empty(cut.FindAll("li.ns-list-editor-open.align-center"));
    }

    static Task Press(IRenderedComponent<ListEditorFormHost> cut, string name)
    {
        return cut.InvokeAsync(() => cut.FindAll($"button[aria-label='{name}']")[0].Click());
    }

    static Task Submit(IRenderedComponent<ListEditorFormHost> cut)
    {
        return cut.InvokeAsync(() => cut.Find("form").Submit());
    }

    static string? RowError(IRenderedComponent<ListEditorFormHost> cut)
    {
        var fields = cut.FindComponents<MudTextField<string>>();

        if (fields.Count == 0)
        {
            return null;
        }

        return fields[0].Instance.Error ? fields[0].Instance.ErrorText : null;
    }

    /// <summary>The row's refusal is posted on the FORM's context, so the document's own submit
    /// is what re-answers it: "placed on submit, cleared on the next submit" (intentional-ui.md)
    /// is one rule for every placement on the context. Left standing it counted as a validation
    /// failure nobody could lift — Guardar ran no handler, drew nothing and said nothing.</summary>
    [Fact]
    public async Task ADocumentSubmitLiftsARowRefusalStandingOnItsContext()
    {
        var cut = Render<ListEditorFormHost>(ps => ps.Add(p => p.Refuse, true));

        await Press(cut, "Agregar");
        await Press(cut, "Confirmar");

        Assert.Equal("Obligatorio", RowError(cut));

        await Submit(cut);

        Assert.Equal(1, cut.Instance.Submits);
        Assert.Null(RowError(cut));
    }

    /// <summary>The other half of the same rule: lifting the row's answer is not letting the row
    /// through quietly. What still refuses re-posts in the very pass that lifted it — the row's
    /// own field here — so the submit either runs or draws its reason where the person is
    /// looking. Mute is the one answer it cannot give.</summary>
    [Fact]
    public async Task ARowStillRefusedByItsOwnFieldRefusesTheSubmitOutLoud()
    {
        var cut = Render<ListEditorFormHost>(ps => ps
            .Add(p => p.Refuse, true)
            .Add(p => p.RowFieldRequired, true));

        await Press(cut, "Agregar");
        await Press(cut, "Confirmar");

        await Submit(cut);

        Assert.Equal(0, cut.Instance.Submits);
        Assert.Equal("Obligatorio", RowError(cut));
    }

    /// <summary>A store's messages outlive the component that posted them: a list that leaves
    /// the screen with a row refused left the form holding a validation failure with no input on
    /// screen to show it and nobody able to lift it — the document could never be saved again,
    /// with nothing explaining why. The list's own end is where it lets go.</summary>
    [Fact]
    public async Task AListUnmountedWithARowRefusedLeavesNothingOnTheFormsContext()
    {
        var cut = Render<ListEditorFormHost>(ps => ps.Add(p => p.Refuse, true));

        await Press(cut, "Agregar");
        await Press(cut, "Confirmar");

        Assert.Equal("Obligatorio", RowError(cut));

        cut.Render(ps => ps.Add(p => p.ListMounted, false));

        await Submit(cut);

        Assert.Equal(1, cut.Instance.Submits);
    }
}
