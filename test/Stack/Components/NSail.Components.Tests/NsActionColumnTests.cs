// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Icons;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>The grid's action column lines up: the column names itself by having nothing to
/// name (no For, no label), the toolbar caps at three and folds the rest into the kebab, and
/// it reserves that kebab's slot whether it renders one or not — which is what keeps a row of
/// one action the same width as a row of three, so every row's first icon starts at the same
/// x. Reported on PartiesPage, where rows carry different action counts.</summary>
public sealed class NsActionColumnTests : BunitContext, IAsyncLifetime
{
    public NsActionColumnTests()
    {
        Services.AddMudServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            ["Common.MoreActions"] = MoreActions,
        })]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsActionColumnTests).Assembly, []));
        Services.AddScoped<RootSurface>();
        Services.AddScoped<SurfaceHistory>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // MudPopoverProvider pulls in a MudBlazor service that is IAsyncDisposable-only and
    // internal, so bUnit's synchronous teardown cannot dispose it — xunit's IAsyncLifetime
    // routes teardown to the async DisposeAsync that can (PolicyAudienceEditorTests' note).
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    static IReadOnlyList<ActionItem> Commands(int count, string prefix = "Act", Enum? group = null)
    {
        return Enumerable
            .Range(1, count)
            .Select(i => new ActionItem { Name = $"{prefix}{i}", OnClick = () => Task.CompletedTask, Group = group })
            .ToList();
    }

    static ActionItem Constant()
    {
        return new ActionItem { Name = "Ficha", OnClick = () => Task.CompletedTask };
    }

    // The overflow's own word, the one a reader meets on the kebab — so the lists below name
    // every control by the same thing, and the kebab is told apart by the word it answers to
    // rather than by the silence it used to be the only control in.
    const string MoreActions = "More actions";

    /// <summary>The kebab is a control like the others: it answers to a name. It used to be
    /// the one button in the row with no label at all — a glyph a screen reader met unnamed
    /// and a pointer met bare — because the toolbar handed NsMenu no Label (nsail#1865).</summary>
    [Fact]
    public void TheKebabAnswersToAName()
    {
        var cut = Row(leading: 5);

        var kebab = cut.Find(".ns-action-toolbar .ns-menu button");

        Assert.Equal(MoreActions, kebab.GetAttribute("aria-label"));
    }

    // Every control the toolbar renders, in DOM order, named by the label a screen reader
    // reads — the kebab among them, under the overflow's own word.
    // nsail#1056: a contributed verb may report the STATE its row is in, and it wears that
    // state's tone in the status channel's own vocabulary — the same classes a word or a dot
    // would wear, so no caller owns a palette. It is a wrapper around the control and not a
    // class on it: both chromes paint themselves with the vendor's inherit colour.
    [Fact]
    public void AVerbThatReportsAStateWearsTheStatusChannelsTone()
    {
        var cut = Render<ActionColumnHost>();

        cut.Render(p => p.Add(
            x => x.Actions,
            new List<ActionItem>
            {
                new() { Name = "Refused", Severity = NsSeverity.Error, OnClick = () => Task.CompletedTask },
            }));

        var toned = cut.Find(".ns-action-toolbar .ns-status-text");

        Assert.Contains("ns-status-error", toned.ClassList);
        Assert.NotNull(toned.QuerySelector("button"));
    }

    // And an act that reports no state carries no tone at all, which is what every act in the
    // app is: the ordinary ink, not a colour somebody has to rule out.
    [Fact]
    public void AnActThatReportsNoStateIsPaintedByNobody()
    {
        var cut = Grid(1);

        Assert.Empty(cut.FindAll(".ns-action-toolbar .ns-status-text"));
    }

    // Unset stays the icon-only row the toolbar was built for — a glyphed command renders
    // through NsAction's own Wordless branch (no .ns-button-label span at all); a host that
    // words its own acts (a dialog's labelled home for a contributed act, ui/actions.md)
    // writes Always and the command carries the word instead of a kit reaching past the
    // presenter to draw one of its own. Commands() hands over no Icon, so this needs its own
    // item — Wordless is never true for a glyphless act (NsButton), whatever the breakpoint.
    static ActionItem Glyphed()
    {
        return new ActionItem { Name = "Act1", Icon = NsIcons.Edit, OnClick = () => Task.CompletedTask };
    }

    [Fact]
    public void UnsetBreakpointLeavesTheCommandWordless()
    {
        var cut = Render<ActionColumnHost>();

        cut.Render(p => p.Add(x => x.Actions, [Glyphed()]));

        Assert.Empty(cut.Find(".ns-action-toolbar").QuerySelectorAll(".ns-button-label"));
    }

    [Fact]
    public void AlwaysWordsTheCommand()
    {
        var cut = Render<ActionColumnHost>();

        cut.Render(p => p
            .Add(x => x.Actions, [Glyphed()])
            .Add(x => x.Breakpoint, NsBreakpoint.Always));

        Assert.Contains("Act1", cut.Find(".ns-action-toolbar .ns-button-label").TextContent, StringComparison.Ordinal);
    }

    static IReadOnlyList<string> Controls(IRenderedComponent<ActionColumnHost> cut)
    {
        return cut
            .Find(".ns-actions .ns-action-toolbar")
            .QuerySelectorAll("button")
            .Select(Named)
            .ToList();
    }

    // The same list with the outlet's loading mark among the controls, in DOM order.
    static IReadOnlyList<string> Strip(IRenderedComponent<ActionColumnHost> cut)
    {
        return cut
            .Find(".ns-actions .ns-action-toolbar")
            .QuerySelectorAll("button, .ns-action-mark")
            .Select(node => node.ClassList.Contains("ns-action-mark") ? "mark" : Named(node))
            .ToList();
    }

    // The overflow reads as "kebab" in the lists above, by the word it carries: what the
    // assertions are about is where it stands in the row, not which string names it.
    static string Named(IElement node)
    {
        return node.GetAttribute("aria-label") switch
        {
            MoreActions => "kebab",
            { } label => label,
            null => "unnamed"
        };
    }

    // MudPopoverProvider only marks the popover service initialized from its own
    // OnAfterRender, and every icon button carries a tooltip — one rendered in the very
    // first pass throws before the grid exists. The grid arrives empty and takes its
    // actions on the pass after, which is also how a real outlet fills.
    IRenderedComponent<ActionColumnHost> Grid(int actions)
    {
        var cut = Render<ActionColumnHost>();

        cut.Render(p => p.Add(x => x.Actions, Commands(actions)));

        return cut;
    }

    IRenderedComponent<ActionColumnHost> Row(
        int leading,
        int contributed = 0,
        bool constant = true,
        int maxVisible = 3,
        bool resolving = false)
    {
        var cut = Render<ActionColumnHost>();

        cut.Render(p => p
            .Add(x => x.Leading, Commands(leading, "Verb"))
            .Add(x => x.Actions, Commands(contributed, "Contributed"))
            .Add(x => x.Constant, constant ? Constant() : null)
            .Add(x => x.MaxVisible, maxVisible)
            .Add(x => x.Resolving, resolving));

        return cut;
    }

    [Fact]
    public void TheColumnThatNamesNothingIsTheActionColumn()
    {
        var cut = Grid(1);

        var headers = cut.FindAll("th").Select(cell => cell.ClassName ?? "").ToList();
        var cells = cut.FindAll("tbody td").Select(cell => cell.ClassName ?? "").ToList();

        Assert.Equal(3, headers.Count);
        Assert.DoesNotContain("ns-actions", headers[0]);
        Assert.DoesNotContain("ns-actions", headers[1]);
        Assert.Contains("ns-actions", headers[2]);

        Assert.Equal(3, cells.Count);
        Assert.DoesNotContain("ns-actions", cells[0]);
        Assert.DoesNotContain("ns-actions", cells[1]);
        Assert.Contains("ns-actions", cells[2]);
    }

    [Fact]
    public void TheColumnReservesTheCapAndTheKebabBesideIt()
    {
        var cut = Grid(1);

        var toolbar = cut.Find(".ns-actions .ns-action-toolbar");

        Assert.Contains("--ns-action-slots: 4", toolbar.GetAttribute("style"));
    }

    [Fact]
    public void ARowThatContributedNothingStillHoldsTheReserve()
    {
        var cut = Grid(0);

        var toolbar = cut.Find(".ns-actions .ns-action-toolbar");

        Assert.Empty(toolbar.QuerySelectorAll("button"));
        Assert.Contains("--ns-action-slots: 4", toolbar.GetAttribute("style"));
    }

    [Fact]
    public void TheStackedLayoutIsReachedThroughTheTablesOwnElement()
    {
        var cut = Grid(1);

        var table = cut.Find(".ns-table");

        // The action column's stacked-layout rules are a compound selector — ours and the
        // vendor's breakpoint class on the same element — so the right-aligned strip below
        // 960px lives or dies on MudTable composing the Class it is handed into the very
        // list it stamps mud-sm-table on. Nothing else in the app would notice if it stopped.
        Assert.Contains("mud-sm-table", table.ClassName);
    }

    [Fact]
    public void TheRowsDoorIsDrawnWithTheOpenGlyphWhateverThePageHandedIn()
    {
        var cut = Render<ActionColumnHost>(p => p
            .Add(x => x.Constant, new ActionItem { Name = "Ficha", PageType = typeof(ProbeOtherPage), Icon = NsIcons.Search }));

        var door = cut.Find(".ns-action-toolbar a");

        Assert.Contains(PathOf(NsIcons.Open), door.InnerHtml);
        Assert.DoesNotContain(PathOf(NsIcons.Search), door.InnerHtml);
    }

    static string PathOf(Glyph glyph)
    {
        return System.Text.RegularExpressions.Regex.Match(glyph.Markup, "d=\"([^\"]+)\"").Groups[1].Value;
    }

    [Fact]
    public void TheOpenGlyphIsNotTheFiltersMagnifier()
    {
        Assert.NotEqual(NsIcons.Search, NsIcons.Open);
    }

    [Fact]
    public void ThreeActionsShowThreeIconsAndNoKebab()
    {
        var cut = Grid(3);

        var toolbar = cut.Find(".ns-actions .ns-action-toolbar");

        Assert.Equal(3, toolbar.QuerySelectorAll("button").Length);
    }

    [Fact]
    public void TheFourthActionFoldsIntoTheKebab()
    {
        var cut = Grid(4);

        var toolbar = cut.Find(".ns-actions .ns-action-toolbar");

        // Three icons plus the kebab's own button: the fourth action is in the menu, not
        // beside the others.
        Assert.Equal(4, toolbar.QuerySelectorAll("button").Length);
        Assert.NotEmpty(toolbar.QuerySelectorAll(".mud-menu"));
    }

    // The sweep of 2026-08-11 (stories.md, "el espacio antes de la lupa"): the page's own
    // fixed verbs stopped standing beside the toolbar and became its Leading, so the cell is
    // ONE toolbar with one cap, one overflow and one reserve — and the row's constant is
    // pinned at the trailing edge of it.

    [Fact]
    public void TheWholeCellIsOneToolbar()
    {
        var cut = Row(leading: 2, contributed: 1);

        Assert.Single(cut.Find("tbody td.ns-actions").QuerySelectorAll(".ns-action-toolbar"));
    }

    [Fact]
    public void TheHostsVerbsComeFirstAndTheConstantLast()
    {
        var cut = Row(leading: 2, contributed: 1);

        Assert.Equal(["Verb1", "Verb2", "Contributed1", "Ficha"], Controls(cut));
    }

    [Fact]
    public void TheConstantNeverFoldsIntoTheKebab()
    {
        var cut = Row(leading: 5);

        // Three verbs, the constant, then the kebab holding the other two: the overflow is
        // what absorbs a long row, never the fixed target — and the kebab is what closes the
        // row now (Leonardo, 2026-08-11: "el kebab cierra la fila"), not the constant.
        Assert.Equal(["Verb1", "Verb2", "Verb3", "Ficha", "kebab"], Controls(cut));

        var menu = cut.Find(".ns-action-toolbar .mud-menu");

        Assert.DoesNotContain("Ficha", menu.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void TheReserveCoversTheCapTheKebabAndTheConstant()
    {
        var toolbar = Row(leading: 1).Find(".ns-actions .ns-action-toolbar");

        Assert.Contains("--ns-action-slots: 5", toolbar.GetAttribute("style"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public void RowsOfferingDifferentCountsPinTheReserveToTheSameWidth(int verbs)
    {
        // Leonardo, 2026-08-10, on Roles: the admin row rendered one lápiz and every other
        // row a lápiz plus a tacho, in two different compositions — "horrendo!", columns
        // zigzagging between rows. One toolbar answers it structurally: whatever the row
        // offers, the reserve is the same width, regardless of which control closes it —
        // the constant when the row does not overflow, the kebab when it does (2026-08-11
        // ruling: "el kebab cierra la fila").
        var cut = Row(leading: verbs);

        var toolbar = cut.Find(".ns-actions .ns-action-toolbar");
        var overflows = verbs > 3;

        Assert.Contains("--ns-action-slots: 5", toolbar.GetAttribute("style"), StringComparison.Ordinal);

        var controls = Controls(cut);

        Assert.Equal("Ficha", controls[overflows ? ^2 : ^1]);
        Assert.Equal(overflows, controls[^1] == "kebab");
    }

    [Fact]
    public void ARowWithNoConstantReservesOneSlotLess()
    {
        var toolbar = Row(leading: 1, constant: false).Find(".ns-actions .ns-action-toolbar");

        Assert.Contains("--ns-action-slots: 4", toolbar.GetAttribute("style"), StringComparison.Ordinal);
    }

    // The cell of a row whose contributors are still answering (nsail#1060). The outlet is what
    // knows it is asking; the mark is drawn here, once, for every outlet there is.

    [Fact]
    public void TheMarkStandsWhereTheContributionWillLand()
    {
        var cut = Row(leading: 2, resolving: true);

        Assert.Equal(["Verb1", "Verb2", "mark", "Ficha"], Strip(cut));
    }

    [Fact]
    public void TheMarkCostsTheReserveNothing()
    {
        var toolbar = Row(leading: 1, resolving: true).Find(".ns-actions .ns-action-toolbar");

        // It stands in a slot the cap already holds for the verb it announces, so the column
        // is exactly as wide while the row is asking as it is once the row knows.
        Assert.Contains("--ns-action-slots: 5", toolbar.GetAttribute("style"), StringComparison.Ordinal);
    }

    [Fact]
    public void ACellWithNothingLeftToAskForCarriesNoMark()
    {
        Assert.Equal(["Verb1", "Contributed1", "Ficha"], Strip(Row(leading: 1, contributed: 1)));
    }

    // Personas (nsail#657): Leonardo asked for one visible verb on a row that offered five, so
    // the cap goes to zero and the constant is the edit. Zero is the honest end of MaxVisible's
    // own range — the constant was never inside the cap, so the cell is still exactly the two
    // boxes it reserves.

    [Fact]
    public void ACapOfZeroLeavesTheConstantAndTheKebabAlone()
    {
        var cut = Row(leading: 3, contributed: 1, maxVisible: 0);

        Assert.Equal(["Ficha", "kebab"], Controls(cut));
        Assert.Contains("--ns-action-slots: 2", cut.Find(".ns-action-toolbar").GetAttribute("style"), StringComparison.Ordinal);
    }

    [Fact]
    public void ACappedRowWithNothingToOfferShowsNoKebab()
    {
        var cut = Row(leading: 0, maxVisible: 0);

        Assert.Equal(["Ficha"], Controls(cut));
    }

    // The kebab's rule (nsail#657): a menu that holds two families of acts reads as families,
    // not as a list, and a contributor lands in "the rest" without declaring anything —
    // ActionItem.Group unset is what the divider separates the declared family FROM.

    async Task<IReadOnlyList<string>> Menu(IRenderedComponent<ActionColumnHost> cut)
    {
        await cut.Find(".ns-action-toolbar .mud-menu button").ClickAsync(new MouseEventArgs());

        return cut
            .Find(".ns-menu-list")
            .QuerySelectorAll(".ns-menu-item, .ns-menu-divider")
            .Select(node => node.ClassName!.Contains("ns-menu-divider", StringComparison.Ordinal) ? "rule" : node.TextContent.Trim())
            .ToList();
    }

    IRenderedComponent<ActionColumnHost> Grouped(int declared, int rest)
    {
        var cut = Render<ActionColumnHost>();

        cut.Render(p => p
            .Add(x => x.Leading, Commands(declared, "Standing", Affinity.Standing).Concat(Commands(rest, "Verb")).ToList())
            .Add(x => x.Constant, Constant())
            .Add(x => x.MaxVisible, 0));

        return cut;
    }

    [Fact]
    public async Task TheKebabIsRuledWhereTheAffinityChanges()
    {
        var rows = await Menu(Grouped(declared: 3, rest: 2));

        Assert.Equal(["Standing1", "Standing2", "Standing3", "rule", "Verb1", "Verb2"], rows);
    }

    // One render per test: a second MudPopoverProvider in the same context re-subscribes the
    // vendor's overlay section and the render throws before the menu exists.

    [Fact]
    public async Task AKebabWhoseItemsAllDeclareTheAffinityIsNotRuled()
    {
        Assert.DoesNotContain("rule", await Menu(Grouped(declared: 4, rest: 0)));
    }

    [Fact]
    public async Task AKebabWhoseItemsDeclareNothingIsNotRuledEither()
    {
        Assert.DoesNotContain("rule", await Menu(Grouped(declared: 0, rest: 4)));
    }

    [Fact]
    public async Task TheRuleNeverStandsAtEitherEndOfTheMenu()
    {
        var rows = await Menu(Grouped(declared: 1, rest: 1));

        Assert.Equal(["Standing1", "rule", "Verb1"], rows);
    }

    // Any enum the caller owns; the Stack names no affinity of its own, because an affinity is
    // domain vocabulary and the Stack has none.
    enum Affinity
    {
        Standing
    }

    // nsail#2696 — the verb that destroys never sits at the plain gap-1 from its neighbour
    // (Catálogo, Users, Medidas measured at 4px). Read off NsIcons.Delete, not a flag a
    // contributor sets, so a row that already calls GetAction(Delete, row, NsIcons.Delete)
    // gets it with nothing changed at the call site.

    [Fact]
    public void TheVerbThatDestroysStandsApartFromItsNeighbour()
    {
        var cut = Render<ActionColumnHost>();

        cut.Render(p => p.Add(x => x.Leading, new List<ActionItem>
        {
            new() { Name = "Edit", Icon = NsIcons.Edit, OnClick = () => Task.CompletedTask },
            new() { Name = "Delete", Icon = NsIcons.Delete, OnClick = () => Task.CompletedTask },
        }));

        var destructive = cut.Find(".ns-action-toolbar .ns-action-destructive");

        Assert.Equal("Delete", destructive.QuerySelector("button")!.GetAttribute("aria-label"));
    }

    // The Stack's own Sample (ContactsPage) draws it the other way round: Delete is the row's
    // contributed Leading verb and Edit is its Constant, so Delete renders FIRST, not last —
    // the one order the fact above never exercises, and the one a first-child-only rule could
    // never reach (nsail#2696, review round 2).
    [Fact]
    public void TheVerbThatDestroysStandsApartWhateverSideItsNeighbourIsOn()
    {
        var cut = Render<ActionColumnHost>();

        cut.Render(p => p
            .Add(x => x.Leading, new List<ActionItem>
            {
                new() { Name = "Delete", Icon = NsIcons.Delete, OnClick = () => Task.CompletedTask },
            })
            .Add(x => x.Constant, new ActionItem { Name = "Edit", Icon = NsIcons.Edit, OnClick = () => Task.CompletedTask }));

        var destructive = cut.Find(".ns-action-toolbar .ns-action-destructive");

        Assert.Equal("Delete", destructive.QuerySelector("button")!.GetAttribute("aria-label"));

        // Delete IS :first-child here (Visible() draws before Constant) — the exact shape
        // :not(:first-child) missed. :only-child is what the rule has to key off instead, so
        // pin the precondition directly: the destructive span is not alone in its row.
        Assert.True(destructive.ParentElement!.Children.Length > 1);
    }

    // An ordinary verb — edit, the ficha, a contributed command — never earns the separation,
    // whatever glyph it carries, so the rule stays Delete's alone.
    [Fact]
    public void AnOrdinaryVerbNeverCarriesTheSeparation()
    {
        var cut = Row(leading: 2);

        Assert.Empty(cut.FindAll(".ns-action-toolbar .ns-action-destructive"));
    }

    // bUnit renders no box model, so the margin the class earns is read off the stylesheet
    // itself. One side each, not margin-inline on both: the
    // toolbar packs to its own trailing edge, so whichever control is last sits on the cell's
    // fixed right border — a margin on THAT side pushes it inside the border the moment
    // Delete itself closes the row (Leading = [Edit, Delete], Constant nil), which is the
    // raggedness this column exists to end. :not(:last-child) pays the trailing gap,
    // :not(:first-child) pays the leading one, and either reduces to :only-child's own
    // exclusion when Delete has no neighbour on that side.
    [Fact]
    public void TheStylesheetSeparatesTheDestructiveVerbOnlyFromTheSideThatHasANeighbour()
    {
        var css = HouseStylesheet.Read();

        var end = css.IndexOf(".ns-action-toolbar .ns-action-destructive:not(:last-child) {", StringComparison.Ordinal);
        Assert.True(end >= 0, "the trailing separation rule was not found in the stylesheet.");
        Assert.Contains("margin-inline-end:", css[end..css.IndexOf('}', end)], StringComparison.Ordinal);

        var start = css.IndexOf(".ns-action-toolbar .ns-action-destructive:not(:first-child) {", StringComparison.Ordinal);
        Assert.True(start >= 0, "the leading separation rule was not found in the stylesheet.");
        Assert.Contains("margin-inline-start:", css[start..css.IndexOf('}', start)], StringComparison.Ordinal);

        // The shape to avoid: margin-inline (both sides at once) on :only-child's
        // own exclusion, which costs the side sitting on the grid's own fixed edge when
        // Delete itself closes the row.
        Assert.DoesNotContain(".ns-action-toolbar .ns-action-destructive:not(:only-child) {", css, StringComparison.Ordinal);
    }

    // nsail#2696 — a system row with no actions measured 33px against 45px for a row beside it
    // that had them: the actions column's own icon button (1.875rem) stands inside the dense
    // table's own 6px-top-and-bottom cell padding, 2.625rem border-box, so the floor has to be
    // that padded number — not the bare button's — or a floor under what a text-only row
    // already measures changes no pixel. Stated for every body cell, not only the actions one,
    // so a row's height never depends on whether its own row draws a button.
    //
    // `height`, not `min-height`: the cell this targets is `display: table-cell` in column
    // mode, where CSS 2.1 §10.7 leaves min-height/max-height undefined — engines differ on
    // whether it ever reaches the row. The regime is the query, never the class: the root wears
    // mud-sm-table at every width (TheStackedLayoutIsReachedThroughTheTablesOwnElement), so a
    // rule excluding it by :not() would match no cell at all, and a stacked phone card would be
    // handed a HEIGHT on every line.
    [Fact]
    public void TheStylesheetFloorsEveryBodyCellToThePaddedActionsCellHeightAboveTheStackingWidth()
    {
        var css = HouseStylesheet.Read();

        Assert.DoesNotContain(":not(.mud-sm-table)", css, StringComparison.Ordinal);

        var query = css.IndexOf("@media (min-width: 960.02px) {", StringComparison.Ordinal);
        Assert.True(query >= 0, "the column-mode row-height query was not found in the stylesheet.");

        var start = css.IndexOf(".ns-table .mud-table-body .mud-table-row .mud-table-cell {", query, StringComparison.Ordinal);
        Assert.True(start >= 0, "the column-mode row-height floor was not found in the stylesheet.");

        var end = css.IndexOf('}', start);
        Assert.Contains("height: 2.625rem;", css[start..end], StringComparison.Ordinal);
        Assert.DoesNotContain("min-height:", css[start..end], StringComparison.Ordinal);
    }

    // The actions icon button grows under a coarse pointer (--ns-action-slot: 3.25rem), so the
    // row floor grows with it or a tablet above the stacking width reads the two kinds of row
    // apart again, in the very regime the story's 44px ask is about.
    [Fact]
    public void TheStylesheetGrowsTheColumnModeRowFloorUnderACoarsePointer()
    {
        var css = HouseStylesheet.Read();

        var coarse = css.IndexOf("--ns-action-slot: 3.25rem;", StringComparison.Ordinal);
        Assert.True(coarse >= 0, "the coarse-pointer action-slot rule was not found in the stylesheet.");

        var query = css.IndexOf("@media (min-width: 960.02px) {", coarse, StringComparison.Ordinal);
        Assert.True(query >= 0, "the coarse-pointer column-mode row-height query was not found in the stylesheet.");

        var start = css.IndexOf(".ns-table .mud-table-body .mud-table-row .mud-table-cell {", query, StringComparison.Ordinal);
        Assert.True(start >= 0, "the coarse-pointer column-mode row-height floor was not found in the stylesheet.");

        var end = css.IndexOf('}', start);
        Assert.Contains("height: 3.75rem;", css[start..end], StringComparison.Ordinal);
        Assert.DoesNotContain("min-height:", css[start..end], StringComparison.Ordinal);
    }

    // The open row's own stacked form (.ns-table-editing under its @container, "the open row
    // stacks where its container cannot seat it") goes through the same flex cell as the phone
    // card, so the column-mode floor above — gated by the viewport, which that @container
    // condition does not narrow — has to be taken back inside it, or every line of a card being
    // edited in a narrow aside stands as tall as a whole dense row.
    [Fact]
    public void TheStylesheetTakesTheRowFloorBackInTheOpenRowsStackedForm()
    {
        var css = HouseStylesheet.Read();

        var container = css.IndexOf("@container (max-width: 959.98px) {", StringComparison.Ordinal);
        Assert.True(container >= 0, "the open row's stacked-form container query was not found in the stylesheet.");

        var start = css.IndexOf(".ns-table.ns-table-editing .mud-table-body .mud-table-row .mud-table-cell {", container, StringComparison.Ordinal);
        Assert.True(start >= 0, "the stacked-editing row-height reset was not found inside the container query.");

        var end = css.IndexOf('}', start);
        Assert.Contains("height: auto;", css[start..end], StringComparison.Ordinal);
    }
}
