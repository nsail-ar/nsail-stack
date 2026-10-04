// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
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
}
