// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>Covers the two things NsTree grew for a policy editor — a tri-state checkbox
/// whose middle state is read off the children instead of stored, and a search that keeps
/// the branch a match was found in — plus the single-selection shape its three existing
/// consumers use, which had to keep behaving identically.</summary>
public sealed class NsTreeTests : BunitContext
{
    public NsTreeTests()
    {
        Services.AddMudServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    static IReadOnlyList<TreeNode> Roots()
    {
        return
        [
            new TreeNode
            {
                Name = "Ventas",
                Children =
                [
                    new TreeNode { Name = "Crédito" },
                    new TreeNode { Name = "Contado" },
                ],
            },
            new TreeNode { Name = "Compras" },
        ];
    }

    // MudBlazor renders the three checkbox states as mud-checkbox-{true|false|null}; null is
    // the indeterminate one.
    static IReadOnlyList<string> States(IRenderedComponent<MarkTreeHost> cut)
    {
        return cut
            .FindAll(".mud-treeview-item-checkbox .mud-checkbox > span")
            .Select(span => span.ClassName!.Split(' ').First(c => c.StartsWith("mud-checkbox-")))
            .ToList();
    }

    static IReadOnlyList<string> Marked(IRenderedComponent<MarkTreeHost> cut)
    {
        return cut.Instance.Marked!.Select(node => node.Name).Order().ToList();
    }

    [Fact]
    public async Task CheckingAParent_ChecksItsChildren()
    {
        var cut = Render<MarkTreeHost>(p => p
            .Add(x => x.Roots, Roots())
            .Add(x => x.Expanded, true));

        // Document order: Ventas, Crédito, Contado, Compras.
        await cut.InvokeAsync(() => cut.FindAll(".mud-treeview-item-checkbox input")[0].Change(true));

        Assert.Equal(["Contado", "Crédito", "Ventas"], Marked(cut));
        Assert.Equal(
            ["mud-checkbox-true", "mud-checkbox-true", "mud-checkbox-true", "mud-checkbox-false"],
            States(cut));
    }

    [Fact]
    public async Task UncheckingOneChild_LeavesTheParentIndeterminate()
    {
        var cut = Render<MarkTreeHost>(p => p
            .Add(x => x.Roots, Roots())
            .Add(x => x.Expanded, true));

        await cut.InvokeAsync(() => cut.FindAll(".mud-treeview-item-checkbox input")[0].Change(true));
        await cut.InvokeAsync(() => cut.FindAll(".mud-treeview-item-checkbox input")[1].Change(false));

        // The middle state is derived and never stored: the parent is simply not in the
        // marked collection any more, and the box reads null off its two children.
        Assert.Equal(["Contado"], Marked(cut));
        Assert.Equal(
            ["mud-checkbox-null", "mud-checkbox-false", "mud-checkbox-true", "mud-checkbox-false"],
            States(cut));
    }

    [Fact]
    public async Task ReCheckingTheLastChild_ChecksTheParentBack()
    {
        var cut = Render<MarkTreeHost>(p => p
            .Add(x => x.Roots, Roots())
            .Add(x => x.Expanded, true));

        await cut.InvokeAsync(() => cut.FindAll(".mud-treeview-item-checkbox input")[1].Change(true));

        Assert.Equal(["mud-checkbox-null", "mud-checkbox-true", "mud-checkbox-false", "mud-checkbox-false"], States(cut));

        await cut.InvokeAsync(() => cut.FindAll(".mud-treeview-item-checkbox input")[2].Change(true));

        Assert.Equal(["Contado", "Crédito", "Ventas"], Marked(cut));
        Assert.Equal(
            ["mud-checkbox-true", "mud-checkbox-true", "mud-checkbox-true", "mud-checkbox-false"],
            States(cut));
    }

    [Fact]
    public void SingleSelectionTree_HasNoCheckboxesAndNoSearchField()
    {
        var cut = Render<SelectTreeHost>(p => p.Add(x => x.Roots, Roots()));

        Assert.Empty(cut.FindAll(".mud-treeview-item-checkbox"));
        Assert.Empty(cut.FindAll("input"));
    }

    [Fact]
    public async Task SingleSelectionTree_RendersItsItemTemplateAndReportsTheClickedRow()
    {
        var cut = Render<SelectTreeHost>(p => p
            .Add(x => x.Roots, Roots())
            .Add(x => x.Expanded, true));

        Assert.Equal(
            ["Ventas", "Crédito", "Contado", "Compras"],
            cut.FindAll(".node-name").Select(row => row.TextContent));

        await cut.InvokeAsync(() => cut.FindAll(".mud-treeview-item-content")[1].Click());

        Assert.Equal("Crédito", cut.Instance.Selected!.Name);
        Assert.Equal(1, cut.Instance.SelectedCount);
    }

    [Fact]
    public void SingleSelectionTree_ShowsDescendantsOnlyWhenExpanded()
    {
        // A branch's children are always in the markup — MudBlazor collapses them with CSS,
        // it does not unmount them — so the toggle button's own label is what says which way
        // the branch is standing.
        var collapsed = Render<SelectTreeHost>(p => p.Add(x => x.Roots, Roots()));

        Assert.Equal("Expand", collapsed.Find(".mud-treeview-item-expand-button").GetAttribute("aria-label"));

        var expanded = Render<SelectTreeHost>(p => p
            .Add(x => x.Roots, Roots())
            .Add(x => x.Expanded, true));

        Assert.Equal("Collapse", expanded.Find(".mud-treeview-item-expand-button").GetAttribute("aria-label"));
    }

    [Fact]
    public async Task SearchWithoutAccents_FindsTheAccentedRowAndKeepsItsParent()
    {
        var cut = Render<MarkTreeHost>(p => p
            .Add(x => x.Roots, Roots())
            .Add(x => x.Searchable, true));

        await Search(cut, "credito", ["Ventas", "Crédito"]);
    }

    [Fact]
    public async Task SearchMatchingAParent_KeepsThatParentAlone()
    {
        var cut = Render<MarkTreeHost>(p => p
            .Add(x => x.Roots, Roots())
            .Add(x => x.Searchable, true));

        await Search(cut, "compras", ["Compras"]);
    }

    [Fact]
    public async Task ClearingTheSearch_BringsTheWholeTreeBack()
    {
        var cut = Render<MarkTreeHost>(p => p
            .Add(x => x.Roots, Roots())
            .Add(x => x.Searchable, true));

        await Search(cut, "credito", ["Ventas", "Crédito"]);
        await Search(cut, string.Empty, ["Ventas", "Crédito", "Contado", "Compras"]);
    }

    [Fact]
    public async Task SearchingDoesNotDropWhatWasAlreadyChecked()
    {
        var cut = Render<MarkTreeHost>(p => p
            .Add(x => x.Roots, Roots())
            .Add(x => x.Searchable, true)
            .Add(x => x.Expanded, true));

        await cut.InvokeAsync(() => cut.FindAll(".mud-treeview-item-checkbox input")[2].Change(true));

        Assert.Equal(["Contado"], Marked(cut));

        // Contado is filtered off the screen here; what the user checked is not part of what
        // the search narrows.
        await Search(cut, "credito", ["Ventas", "Crédito"]);

        Assert.Equal(["Contado"], Marked(cut));
    }

    [Fact]
    public void ATreeWithoutSearchable_RendersNoSearchField()
    {
        var cut = Render<MarkTreeHost>(p => p.Add(x => x.Roots, Roots()));

        Assert.Empty(cut.FindAll("input[type='text']"));
    }

    // The rows the tree is showing, in document order — a row the filter hid renders nothing
    // at all, so absence from this list is what "hidden" looks like.
    static IReadOnlyList<string> Shown(IRenderedComponent<MarkTreeHost> cut)
    {
        return cut
            .FindAll("li.mud-treeview-item .mud-treeview-item-content p")
            .Select(row => row.TextContent)
            .ToList();
    }

    // NsSearchField debounces (Immediate, no onchange), so the filter lands a moment after
    // the keystroke — every search assertion waits for it rather than reading the DOM
    // straight after the input. bUnit's default WaitFor timeout (1s) flaked twice on
    // GitHub-hosted CI runners against a 150ms debounce; the generous timeout below buys
    // headroom for slow CI hardware without weakening what the assertion demands.
    static async Task Search(IRenderedComponent<MarkTreeHost> cut, string term, IReadOnlyList<string> shown)
    {
        await cut.InvokeAsync(() => cut.Find(".mud-input-slot").Input(term));

        cut.WaitForAssertion(() => Assert.Equal(shown, Shown(cut)), TimeSpan.FromSeconds(10));
    }
}
