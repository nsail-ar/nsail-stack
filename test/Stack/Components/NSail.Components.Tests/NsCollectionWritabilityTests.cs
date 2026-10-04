// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#1314: a collection host is not a field, so the form's writability cascade —
/// the one every field reads (NsFieldBase.IsReadOnly) and every picker reads on top of it
/// (NsPickerBase) — reached the inputs inside an open row and nothing that OFFERS one. Inside
/// an NsForm ReadOnly the nine row editors in the kits still drew their +, their pencil and
/// their inline editor, each gating on its own ReadOnly parameter alone.
///
/// What is held here is the host's own answer: every control NsTable and NsListEditor draw to
/// CHANGE a collection goes with the form's word (NsCollectionBase), including the door a page
/// opens from chrome of its own. The rule lives here rather than in each caller because the
/// controls do — ui/hosts.md, "no caller repeats that rule".</summary>
public sealed class NsCollectionWritabilityTests : BunitContext, IAsyncLifetime
{
    public NsCollectionWritabilityTests()
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
        })]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // MudPopoverProvider resolves a MudBlazor service that is IAsyncDisposable-only, so bUnit's
    // synchronous teardown cannot dispose it (NsListEditorTests' own note).
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    static IReadOnlyList<IElement> Adders(IRenderedComponent<CollectionFormHost> cut)
    {
        return cut.FindAll("button[aria-label='Agregar']");
    }

    static void AssertNoWayIn(IRenderedComponent<CollectionFormHost> cut)
    {
        Assert.Empty(cut.FindAll("button[aria-label='Agregar']"));
        Assert.Empty(cut.FindAll("button[aria-label='Editar']"));
        Assert.Empty(cut.FindAll("button[aria-label='Eliminar']"));
        Assert.Empty(cut.FindAll("tbody input"));
        Assert.Empty(cut.FindAll("li.ns-list-editor-item input"));

        // What the collections are FOR still reads: a form nobody may write to is not a form
        // with nothing in it.
        Assert.Contains("Ana", cut.Find("tbody").TextContent, StringComparison.Ordinal);
        Assert.Contains("Bruno", cut.Find(".item-name").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void AWritableFormLeavesBothCollectionsTheirWayIn()
    {
        var cut = Render<CollectionFormHost>();

        Assert.Equal(2, Adders(cut).Count);
        Assert.NotEmpty(cut.FindAll("button[aria-label='Editar']"));
        Assert.NotEmpty(cut.FindAll("button[aria-label='Eliminar']"));
    }

    [Fact]
    public async Task AWritableFormOpensTheInlineEditorOnBothHosts()
    {
        var cut = Render<CollectionFormHost>();

        await cut.InvokeAsync(() => Adders(cut)[0].Click());
        await cut.InvokeAsync(() => Adders(cut)[1].Click());

        Assert.NotEmpty(cut.FindAll("tbody input"));
        Assert.NotEmpty(cut.FindAll("li.ns-list-editor-item input"));
    }

    [Fact]
    public void AReadOnlyFormLeavesNeitherEditorNorAdder()
    {
        var cut = Render<CollectionFormHost>(p => p.Add(x => x.ReadOnly, true));

        AssertNoWayIn(cut);
    }

    [Fact]
    public void ADisabledFormLeavesNeitherEditorNorAdder()
    {
        var cut = Render<CollectionFormHost>(p => p.Add(x => x.Disabled, true));

        AssertNoWayIn(cut);
    }

    /// <summary>The acts riding the list's bar are ways in too, so the form takes them with the
    /// + — the same sentence the list's own ReadOnly already answered (NsListEditorTests).</summary>
    [Fact]
    public void AReadOnlyFormTakesTheContributedActsWithTheAdder()
    {
        var cut = Render<CollectionFormHost>();

        Assert.Contains("Traer", cut.Markup, StringComparison.Ordinal);

        cut.Render(p => p.Add(x => x.ReadOnly, true));

        Assert.DoesNotContain("Traer", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>The other door: a page that adds from chrome of its own (Nueva Venta's tab
    /// strip) calls the host directly, so gating the + alone would leave a way in that renders
    /// nowhere the form can see.</summary>
    [Fact]
    public async Task AReadOnlyFormShutsTheHostsOwnAddDoorToo()
    {
        var cut = Render<CollectionFormHost>(p => p.Add(x => x.ReadOnly, true));

        await cut.InvokeAsync(cut.Instance.AddThroughTheTable);
        await cut.InvokeAsync(cut.Instance.AddThroughTheList);

        Assert.Single(cut.Instance.Rows);
        Assert.Single(cut.Instance.Items);
        Assert.Empty(cut.FindAll("tbody input"));
        Assert.Empty(cut.FindAll("li.ns-list-editor-item input"));
    }

    /// <summary>A form turns read-only under an open row every time one is submitted — the
    /// cascade carries the freeze NsForm puts on its fields while a submit runs — so the open
    /// editor has to go back to its reading row rather than stand there with its confirm.</summary>
    [Fact]
    public async Task AFormThatTurnsReadOnlyClosesWhatWasOpen()
    {
        var cut = Render<CollectionFormHost>();

        await cut.InvokeAsync(() => Adders(cut)[0].Click());
        await cut.InvokeAsync(() => Adders(cut)[1].Click());

        Assert.NotEmpty(cut.FindAll("tbody input"));
        Assert.NotEmpty(cut.FindAll("li.ns-list-editor-item input"));

        cut.Render(p => p.Add(x => x.ReadOnly, true));

        Assert.Empty(cut.FindAll("tbody input"));
        Assert.Empty(cut.FindAll("li.ns-list-editor-item input"));
        Assert.Empty(cut.FindAll("button[aria-label='Confirmar']"));
    }
}
