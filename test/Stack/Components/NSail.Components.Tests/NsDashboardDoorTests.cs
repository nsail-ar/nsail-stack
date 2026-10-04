// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

[Route("/dashboard-door/open")]
public sealed class OpenDoorTestPage : ComponentBase;

[Route("/dashboard-door/admin")]
[Microsoft.AspNetCore.Authorization.Authorize(Roles = "admin")]
public sealed class AdminDoorTestPage : ComponentBase;

/// <summary>A card as bare as one gets: an NsCard with a header and nothing else, so what a
/// render shows in the header's action slot came from the cascade and from nowhere in this
/// card's own markup.</summary>
public sealed class DoorlessTestCard : ComponentBase
{
    [Parameter]
    public NSail.Icons.Glyph? Icon { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<NsCard>(0);
        builder.AddComponentParameter(1, nameof(NsCard.Header), (RenderFragment)(header => header.AddContent(0, "Card")));
        builder.CloseComponent();
    }
}

/// <summary>A card whose content is itself a card — the composition NsCard's own null cascade
/// exists for.</summary>
public sealed class NestingTestCard : ComponentBase
{
    [Parameter]
    public NSail.Icons.Glyph? Icon { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<NsCard>(0);
        builder.AddComponentParameter(1, nameof(NsCard.Header), (RenderFragment)(header => header.AddContent(0, "Outer")));
        builder.AddComponentParameter(2, nameof(NsCard.Content), (RenderFragment)(content =>
        {
            content.OpenComponent<NsCard>(0);
            content.AddComponentParameter(1, nameof(NsCard.Header), (RenderFragment)(inner => inner.AddContent(0, "Inner")));
            content.CloseComponent();
        }));
        builder.CloseComponent();
    }
}

/// <summary>The door a card gets for declaring a PageType: NsDashboard resolves and gates it,
/// cascades it (CardDoor), and NsCard draws it at the end of its own header row -- so a card
/// gains a door with no markup change of its own and nothing is positioned against its box
/// (nsail#925). Gated by the destination page's own authorize attributes -- the same single
/// gate the nav menu, a lookup's create entry and NsPageLink itself ask -- so a card visible
/// to a session that cannot open its destination renders with no door rather than a dead
/// one.</summary>
public sealed class NsDashboardDoorTests : BunitContext, IAsyncLifetime
{
    sealed class TestContributor(DashboardItem item) : IDashboardContributor
    {
        public Task<IReadOnlyList<DashboardItem>> GetItems()
        {
            return Task.FromResult<IReadOnlyList<DashboardItem>>([item]);
        }
    }

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    BunitAuthorizationContext SetupFor()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsDashboardDoorTests).Assembly, []));
        Services.AddScoped<RootSurface>();
        Services.AddScoped<SurfaceHistory>();
        Services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        Services.AddScoped<PageGate>();
        JSInterop.Mode = JSRuntimeMode.Loose;

        return this.AddAuthorization();
    }

    [Fact]
    public void ACardWithNoPageType_RendersNoDoor()
    {
        var auth = SetupFor();
        auth.SetAuthorized("someone");

        Services.AddSingleton<IDashboardContributor>(new TestContributor(
            new DashboardItem { Name = "NoDoor", CardType = typeof(DoorlessTestCard) }));

        var cut = Render<NsDashboard>();

        Assert.Empty(cut.FindAll(".ns-card-header .mud-card-header-actions"));
    }

    [Fact]
    public void ACardWhoseDestinationTheSessionMayOpen_RendersTheDoor()
    {
        var auth = SetupFor();
        auth.SetAuthorized("someone");

        Services.AddSingleton<IDashboardContributor>(new TestContributor(
            new DashboardItem { Name = "HasDoor", CardType = typeof(DoorlessTestCard), PageType = typeof(OpenDoorTestPage) }));

        var cut = Render<NsDashboard>();

        var door = cut.Find(".ns-card-header .mud-card-header-actions a");

        // The rendered href carries no surface query at all: a dashboard door opens the full
        // page, never a panel over the home it stands on -- what this pins is the raw route
        // NsDashboard derived from the route table, read off the DEBUG probe the same way
        // NsLinkDomProbeTests does: the probe exists only under DEBUG, so RELEASE pins its
        // absence instead, exactly as that precedent does.
#if DEBUG
        Assert.Contains("raw:/dashboard-door/open", door.GetAttribute("data-ns-probe"), StringComparison.Ordinal);
#else
        Assert.False(door.HasAttribute("data-ns-probe"));
#endif
    }

    // Every dashboard card's door opens as Main, with no exception a card can declare
    // (nsail#1326, Leonardo, 2026-09-16: "todos en main"). Read off the resolved href, which
    // carries no surface query at all.
    [Fact]
    public void ACardThatNamesNoTarget_OpensTheFullPage()
    {
        var auth = SetupFor();
        auth.SetAuthorized("someone");

        Services.AddSingleton<IDashboardContributor>(new TestContributor(
            new DashboardItem { Name = "HasDoor", CardType = typeof(DoorlessTestCard), PageType = typeof(OpenDoorTestPage) }));

        var cut = Render<NsDashboard>();

        var door = cut.Find(".ns-card-header .mud-card-header-actions a");

        Assert.Equal("/dashboard-door/open", Uri.UnescapeDataString(door.GetAttribute("href") ?? string.Empty));
    }

    // Being visible on the dashboard and being reachable are two different gates -- the card's
    // own read may allow a session the destination page's own authorize attributes do not.
    [Fact]
    public void ACardWhoseDestinationTheSessionMayNotOpen_RendersNoDoor()
    {
        var auth = SetupFor();
        auth.SetAuthorized("clerk");
        auth.SetRoles("clerk");

        Services.AddSingleton<IDashboardContributor>(new TestContributor(
            new DashboardItem { Name = "Locked", CardType = typeof(DoorlessTestCard), PageType = typeof(AdminDoorTestPage) }));

        var cut = Render<NsDashboard>();

        Assert.Empty(cut.FindAll(".ns-card-header .mud-card-header-actions"));
    }

    // The card's own translated title, not the destination page's -- the door stands in the
    // header row of a card that already carries its own title, so the name it whispers is the
    // one the reader is looking at. Icon-only, so the name is carried as the accessible one
    // rather than drawn: it is what the ns-square whisper prints (ns-mud.css).
    [Fact]
    public void TheDoorsAccessibleNameIsTheCardsOwnTranslatedTitle()
    {
        var auth = SetupFor();
        auth.SetAuthorized("someone");

        Services.AddSingleton<IDashboardContributor>(new TestContributor(
            new DashboardItem { Name = "HasDoor", CardType = typeof(DoorlessTestCard), PageType = typeof(OpenDoorTestPage) }));

        var cut = Render<NsDashboard>();

        var expected = Services.GetRequiredService<StringManager>()
            .Translate(Services.GetRequiredService<MetadataProvider>().KeyFor(typeof(DoorlessTestCard), "Title"));

        var door = cut.Find(".ns-card-header .mud-card-header-actions a");

        Assert.Equal(expected, door.GetAttribute("aria-label"));
        Assert.Empty(door.QuerySelector(".ns-button-label")!.TextContent);
    }

    /// <summary>The door is the house's one button at Default with no word to draw: a square
    /// with that intention's grey fill and its shadow, never the bare glyph floating outside
    /// the row, never the soft accent a card's own act wears, and never the full-strength one,
    /// which is the screen's to spend (nsail#925).</summary>
    [Fact]
    public void TheDoorIsADefaultSquare()
    {
        var auth = SetupFor();
        auth.SetAuthorized("someone");

        Services.AddSingleton<IDashboardContributor>(new TestContributor(
            new DashboardItem { Name = "HasDoor", CardType = typeof(DoorlessTestCard), PageType = typeof(OpenDoorTestPage) }));

        var cut = Render<NsDashboard>();

        var door = cut.Find(".ns-card-header .mud-card-header-actions a");

        Assert.Contains("ns-square", door.ClassList);
        Assert.Contains("mud-button-filled", door.ClassList);
        Assert.DoesNotContain("mud-button-filled-primary", door.ClassList);
        Assert.DoesNotContain("ns-important", door.ClassList);
    }

    /// <summary>A card nested inside another card's content draws no second door: the cascade
    /// belongs to the card whose header carries it, and NsCard hands its content a null one.
    /// Without that, a composed card would sprout a lupa in the middle of another card.</summary>
    [Fact]
    public void ACardNestedInsideAnothersContent_DrawsNoSecondDoor()
    {
        var auth = SetupFor();
        auth.SetAuthorized("someone");

        Services.AddSingleton<IDashboardContributor>(new TestContributor(
            new DashboardItem { Name = "HasDoor", CardType = typeof(NestingTestCard), PageType = typeof(OpenDoorTestPage) }));

        var cut = Render<NsDashboard>();

        Assert.Single(cut.FindAll(".ns-card-header .mud-card-header-actions"));
    }
}
