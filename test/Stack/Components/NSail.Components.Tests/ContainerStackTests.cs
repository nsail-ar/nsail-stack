// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Components.Tests.Fixtures;

namespace NSail.Components.Tests;

/// <summary>A list narrowed by an aside on a wide screen stacks its rows as cards: the vendor's
/// stack asks the viewport, so the house's own copy asks the container, under
/// <c>@media (min-width: 960px) { @container (max-width: 739.98px) }</c>: past the widest aside
/// that cannot seat a table (Lg, 720px), short of the narrowest main surface. bUnit runs no query, so what is pinned is the
/// stylesheet itself — every table reached, not only one with a row open — and the readers that
/// drop with the stack.</summary>
public sealed class ContainerStackTests
{
    const string Query = "@media (min-width: 960px) {\n    @container (max-width: 739.98px) {\n";

    static string StackBlock(string css)
    {
        var start = css.IndexOf(Query + "        .ns-table.mud-sm-table .mud-table-head .mud-table-row {", StringComparison.Ordinal);
        Assert.True(start >= 0, "the container's stacked block was not found in the stylesheet.");

        var end = css.IndexOf("\n}\n", start, StringComparison.Ordinal);

        return css[start..end];
    }

    [Fact]
    public void EveryTableStacksByItsContainerNotOnlyOneWithARowOpen()
    {
        var block = StackBlock(HouseStylesheet.Read());

        Assert.Contains(".ns-table.mud-sm-table .mud-table-cell {\n            display: flex;", block, StringComparison.Ordinal);
        Assert.Contains("content: attr(data-label);", block, StringComparison.Ordinal);
        Assert.DoesNotContain(".ns-table-editing", block, StringComparison.Ordinal);
    }

    /// <summary>The phone card's corrections come along, or the aside's card is a second shape:
    /// the action strip packs right with its reserve gone, the foot keeps its own card with no
    /// blank lines, the open row is still a form, and column importance still hides.</summary>
    [Fact]
    public void TheContainersCardIsThePhonesShape()
    {
        var block = StackBlock(HouseStylesheet.Read());

        Assert.Contains(".ns-table.mud-sm-table .ns-actions {", block, StringComparison.Ordinal);
        Assert.Contains(".ns-table.mud-sm-table .ns-action-toolbar {\n            min-width: 0;", block, StringComparison.Ordinal);
        Assert.Contains(".ns-table.mud-sm-table .mud-table-foot .mud-table-cell:empty {", block, StringComparison.Ordinal);
        Assert.Contains(".ns-table.mud-sm-table .ns-foot-label::before {", block, StringComparison.Ordinal);
        Assert.Contains(".ns-table.mud-sm-table .mud-table-cell.ns-cell-editing {", block, StringComparison.Ordinal);
        Assert.Contains(".ns-table.mud-sm-table .mud-table-cell.d-c-none {\n            display: none;", block, StringComparison.Ordinal);
    }

    /// <summary>A paged grid stacked in an aside grows to its cards like the phone's does, or its
    /// pager is pinned over the last one.</summary>
    [Fact]
    public void APagedGridStackedByItsContainerGrowsToItsRows()
    {
        var block = StackBlock(HouseStylesheet.Read());

        Assert.Contains(".ns-table.ns-table-pages.mud-sm-table,\n        .ns-table-loading-host:has(> .ns-table-pages.mud-sm-table) {\n            min-height: auto;", block, StringComparison.Ordinal);
    }

    /// <summary>The compact line asks the container alone: a table under 600px of container is
    /// stacked at any window, so a viewport gate would leave a compact list in an aside as
    /// cards.</summary>
    [Fact]
    public void TheCompactLineAsksTheContainerAlone()
    {
        var css = HouseStylesheet.Read();

        Assert.Contains("@container (max-width: 599.98px) {\n    .ns-table.ns-table-compact.mud-sm-table:not(.ns-table-editing) .mud-table-body {", css, StringComparison.Ordinal);
        Assert.DoesNotContain("@media (max-width: 960px) {\n    @container (max-width: 599.98px) {", css, StringComparison.Ordinal);
    }

    /// <summary>The add row's mirrors of the table's columns drop wherever the columns do.</summary>
    [Theory]
    [InlineData("        .ns-grid > .ns-select-gutter {\n            display: none !important;")]
    [InlineData("        .ns-grid > *:has(> .ns-row-submit) {\n            --ns-field-width: initial;")]
    [InlineData("        .ns-table.ns-row-submit-gutter thead th:last-child,\n        .ns-table.ns-row-submit-gutter tbody td:last-child {\n            width: auto;")]
    public void AnAddRowsMirrorDropsWithTheContainersStack(string rule)
    {
        var css = HouseStylesheet.Read();

        var at = css.IndexOf(rule, StringComparison.Ordinal);
        Assert.True(at >= 0, $"'{rule}' was not found in the stylesheet.");

        var query = css.LastIndexOf(Query, at, StringComparison.Ordinal);
        Assert.True(query >= 0 && css.IndexOf("\n}\n", query, StringComparison.Ordinal) > at,
            "the rule is not inside the container's stacking query.");
    }
}
