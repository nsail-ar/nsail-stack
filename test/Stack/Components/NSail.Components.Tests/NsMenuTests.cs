// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using AngleSharp.Dom;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>The anchored menu the Stack had no word for (nsail#438): a trigger that stays in
/// place and a list that opens against it. What it holds is items (NsMenuItem) and blocks
/// (NsMenuBlock) — and a block is not an item: no click, no tab stop, presentational to the
/// list, which is what makes it non-interactive by construction rather than by a caller
/// remembering not to wire it.</summary>
public sealed class NsMenuTests : BunitContext, IAsyncLifetime
{
    public NsMenuTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddSingleton<IStringSource>(new FixedStrings(new Dictionary<string, string>
        {
            ["Actions.Profile"] = "My Profile",
        }));
        Services.AddSingleton<StringCatalog>();
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsMenuTests).Assembly, []));
        Services.AddScoped<RootSurface>();
        Services.AddScoped<SurfaceHistory>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // MudPopoverProvider resolves a MudBlazor service that is IAsyncDisposable-only and
    // internal, so bUnit's synchronous teardown cannot dispose it (the same note every
    // popover-hosting fixture in this project carries).
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    async Task<IRenderedComponent<MenuHost>> Open(EventCallback onSwitch = default)
    {
        var cut = Render<MenuHost>(parameters => parameters.Add(host => host.OnSwitch, onSwitch));

        await cut.Find(".mud-menu button").ClickAsync(new MouseEventArgs());

        return cut;
    }

    IElement Row(IRenderedComponent<MenuHost> cut, string text)
    {
        return cut.FindAll(".mud-menu-item").Single(item => item.TextContent.Contains(text, StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheItemsExistOnlyOnceTheTriggerIsClicked()
    {
        var cut = Render<MenuHost>();

        Assert.DoesNotContain("Alpha", cut.Markup, StringComparison.Ordinal);

        await cut.Find(".mud-menu button").ClickAsync(new MouseEventArgs());

        Assert.Contains("Alpha", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Beta", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ABlockIsNotAnItem()
    {
        var cut = await Open();

        var identity = cut.Find(".probe-identity");
        var version = cut.Find(".probe-version");

        // Neither block is a menu item: no click handler, no tab stop, and marked
        // presentational so the list it renders inside offers no option that does nothing.
        Assert.Null(identity.Closest(".mud-menu-item"));
        Assert.Null(version.Closest(".mud-menu-item"));
        Assert.Equal("presentation", identity.ParentElement!.GetAttribute("role"));
        Assert.Equal("presentation", version.ParentElement!.GetAttribute("role"));
    }

    /// <summary>The list is a menu and its rows are menu items, and none of that is written
    /// here: MudMenu renders role="menu" and MudMenuItem role="menuitem", so a row that wrote a
    /// role of its own could only disagree with the list it lands in. A verb row writes nothing
    /// at all.</summary>
    [Fact]
    public async Task EveryRowIsAMenuItemOfTheMenuItLandsIn()
    {
        var cut = await Open();

        Assert.Equal("menu", cut.Find(".ns-menu-list").GetAttribute("role"));

        var verb = Row(cut, "My Profile");

        Assert.Equal("menuitem", verb.GetAttribute("role"));
        Assert.Null(verb.GetAttribute("aria-checked"));
    }

    /// <summary>The check that marks the row in force is an aria-hidden glyph, so the state is
    /// drawn and never announced unless the row says it itself — and inside a menu, saying it is
    /// aria-checked on a menuitemradio: menuitem carries no state at all. Both alternatives
    /// answer, because a row that is one of a choice and lost is not the same as a verb.</summary>
    [Fact]
    public async Task TheRowInForceAnnouncesItselfChecked()
    {
        var cut = await Open();

        Assert.Equal("menuitemradio", Row(cut, "Alpha").GetAttribute("role"));
        Assert.Equal("true", Row(cut, "Alpha").GetAttribute("aria-checked"));

        Assert.Equal("menuitemradio", Row(cut, "Beta").GetAttribute("role"));
        Assert.Equal("false", Row(cut, "Beta").GetAttribute("aria-checked"));
    }

    /// <summary>The second level reads like the first: the rows a submenu opens are the same
    /// component and carry the same role, which is what AC #3 of nsail#1281 asks for — the
    /// shape the supplier picker (nsail#1277) puts in front of a user.</summary>
    [Fact]
    public async Task ASubmenusRowsAreMenuItemsToo()
    {
        var cut = await Open();

        await Row(cut, "Change organization").ClickAsync(new MouseEventArgs());

        var nested = Row(cut, "Gamma");

        Assert.Equal("menuitemradio", nested.GetAttribute("role"));
        Assert.Equal("menu", cut.FindAll(".ns-menu-list")[1].GetAttribute("role"));
    }

    /// <summary>The two sites nsail#1281 could not reach from here, both the vendor's own now: a
    /// submenu's activator is a row of the list it stands in AND a thing that opens a second one,
    /// and it has to say both or a screen reader offers the rows behind it to nobody.</summary>
    [Fact]
    public async Task ASubmenusActivatorIsARowThatAnnouncesTheMenuItOpens()
    {
        var cut = await Open();

        var activator = Row(cut, "Change organization");

        Assert.Equal("menuitem", activator.GetAttribute("role"));
        Assert.Equal("menu", activator.GetAttribute("aria-haspopup"));
        Assert.Equal("false", activator.GetAttribute("aria-expanded"));

        await activator.ClickAsync(new MouseEventArgs());

        activator = Row(cut, "Change organization");

        Assert.Equal("true", activator.GetAttribute("aria-expanded"));
        Assert.Equal(cut.FindAll(".ns-menu-list")[1].GetAttribute("id"), activator.GetAttribute("aria-controls"));
    }

    /// <summary>And the trigger, the other half of the same silence: it says it opens a menu,
    /// whether it is open, and which list it opens.</summary>
    [Fact]
    public async Task TheTriggerAnnouncesTheMenuItOpens()
    {
        var cut = Render<MenuHost>();

        var trigger = cut.Find(".mud-menu button");

        Assert.Equal("menu", trigger.GetAttribute("aria-haspopup"));
        Assert.Equal("false", trigger.GetAttribute("aria-expanded"));
        Assert.Equal("Session", trigger.GetAttribute("aria-label"));

        await trigger.ClickAsync(new MouseEventArgs());

        trigger = cut.Find(".mud-menu button");

        Assert.Equal("true", trigger.GetAttribute("aria-expanded"));
        Assert.Equal(cut.Find(".ns-menu-list").GetAttribute("id"), trigger.GetAttribute("aria-controls"));
    }

    /// <summary>A rule between two families of rows is structure, not an offer: the list holds
    /// it as a separator so nothing arrows onto a row that does nothing.</summary>
    [Fact]
    public async Task ADividerIsNotAnItem()
    {
        var cut = await Open();

        var divider = Assert.Single(cut.FindAll(".ns-menu-divider"));

        Assert.Equal("separator", divider.GetAttribute("role"));
        Assert.Null(divider.GetAttribute("tabindex"));
    }

    [Fact]
    public async Task OnlyTheSelectedRowDrawsACheck()
    {
        var cut = await Open();

        var check = Assert.Single(cut.FindAll(".ns-menu-item-check"));
        var row = check.Closest(".ns-menu-item-row");

        Assert.NotNull(row);
        Assert.Contains("Alpha", row.TextContent, StringComparison.Ordinal);
    }

    /// <summary>A row whose text is data (an organization's name) overrides the derived label;
    /// a row that is a verb does not, and resolves "Actions.{Name}" like every other action.</summary>
    [Fact]
    public async Task ARowWithoutALabelResolvesTheActionsOwnString()
    {
        var cut = await Open();

        Assert.Contains("My Profile", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ANestedMenuOpensAsASubmenu()
    {
        var cut = await Open();

        // The nested menu renders as a row of the open list, not as a second trigger beside
        // the first: the vendor reads the nesting and this component writes nothing for it.
        var nested = Row(cut, "Change organization");

        Assert.Contains("mud-menu-sub-menu-activator", nested.ClassName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACommandRowRunsItsAction()
    {
        var ran = 0;
        var cut = await Open(EventCallback.Factory.Create(this, () => ran++));

        await Row(cut, "Beta").ClickAsync(new MouseEventArgs());

        Assert.Equal(1, ran);
    }

    /// <summary>Choosing a row closes the menu, which unmounts everything that drew it — a
    /// kit's whole menu body is a component of its own and goes with it. A command that runs
    /// on any Runner belonging to that tree is already disposed when the handler starts and
    /// vanishes without a word, which is the one thing a failure must not do; the row supplies
    /// a run of its own so the work outlives the menu.</summary>
    [Fact]
    public async Task ACommandOutlivesTheMenuItClosed()
    {
        var ran = 0;
        var cut = await Open(EventCallback.Factory.Create(this, () => ran++));

        var detached = Row(cut, "Detached");

        await detached.ClickAsync(new MouseEventArgs());

        Assert.Equal(1, ran);
    }

    /// <summary>nsail#1288: a Content face's click keeps the SAME handler across the tree's
    /// renders. A menu seated in a popover is re-rendered while it is being used — the vendor's
    /// own churn, thirteen renders a second with a list open against two idle — and a click
    /// wired to a delegate the render itself built is issued a fresh handler id by every one of
    /// them, so the gesture a caller already holds is aimed at a handler the renderer dropped.</summary>
    [Fact]
    public async Task AContentFacesClickKeepsItsHandlerAcrossRenders()
    {
        var cut = Render<MenuFaceHost>();

        var face = cut.Find(".ns-menu-activator");

        // The id Blazor issued for this element's click. It is what a dispatch — bUnit's and
        // the browser's alike — is aimed at, so it is the gesture's identity and not plumbing.
        var handler = face.GetAttribute("blazor:onclick");

        cut.Render(parameters => parameters.Add(host => host.Name, "sku-2"));

        Assert.Equal("sku-2", cut.Find(".probe-face").TextContent);
        Assert.Equal(handler, cut.Find(".ns-menu-activator").GetAttribute("blazor:onclick"));

        // And the face found BEFORE that render still opens the menu, which is the consequence
        // the tests downstream of this component live on.
        await cut.InvokeAsync(() => face.ClickAsync(new MouseEventArgs()));

        Assert.Equal(2, cut.FindAll(".mud-menu-item").Count);
    }

    /// <summary>nsail#1304: a Content face's trigger is ONE element to a reader. The vendor's own
    /// wrapper is the one that carries the tab stop and role="button", and it takes its name from
    /// the face's aria-label through name-from-content — so the face inside it carries neither a
    /// tabindex nor a role, or the same name would be answered by two nested nodes: a button
    /// inside a button to a screen reader, and an ambiguous locator to any strict reach by role.
    /// Whether Chromium will actually FOCUS that wrapper is a layout fact this layer cannot see
    /// (it refuses display:contents), and it is proven in a browser — MenuFaceFocusTests.</summary>
    [Fact]
    public void TheFacesTriggerIsOneElementToAReader()
    {
        var cut = Render<MenuFaceHost>();

        var trigger = cut.Find(".mud-menu-activator");
        var face = cut.Find(".ns-menu-activator");

        Assert.Equal("0", trigger.GetAttribute("tabindex"));
        Assert.Equal("button", trigger.GetAttribute("role"));
        Assert.Null(trigger.GetAttribute("aria-label"));

        Assert.Equal("Suppliers", face.GetAttribute("aria-label"));
        Assert.Null(face.GetAttribute("tabindex"));
        Assert.Null(face.GetAttribute("role"));
    }

    /// <summary>A row runs from inside the vendor's popover tree, which is a sibling of the
    /// router: no surface cascade reaches in, so the root surface has to answer for the link's
    /// target or the address silently degrades to a bare route.</summary>
    [Fact]
    public async Task ALinkRowFollowsItsDestination()
    {
        var cut = await Open();

        var profile = Row(cut, "My Profile");

        await profile.ClickAsync(new MouseEventArgs());

        var navigation = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();

        Assert.EndsWith("/probe/admin", navigation.Uri, StringComparison.Ordinal);
    }
}
