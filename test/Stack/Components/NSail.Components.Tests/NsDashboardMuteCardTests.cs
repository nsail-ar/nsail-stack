// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Icons;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>A card is free to answer with nothing — the pattern every card in the repo
/// follows is "render no content until the read has answered", and an onboarding card with
/// nothing left to ask renders nothing at all, for good. What this pins is that the cell such
/// a card leaves behind collapses: the wrapper is still in the markup, but it holds no card,
/// and the stylesheet takes it out of the grid.</summary>
public sealed class NsDashboardMuteCardTests : BunitContext, IAsyncLifetime
{
    // The selector the stylesheet hides the cell with, restated here so the DOM and the rule
    // are read against the same question rather than two that drifted apart.
    const string Collapsed = ".ns-dashboard-item:not(:has(> .ns-dashboard-card > *))";

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

    IRenderedComponent<NsDashboard> Show(DashboardItem item)
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsDashboardMuteCardTests).Assembly, []));
        Services.AddScoped<RootSurface>();
        Services.AddScoped<SurfaceHistory>();
        Services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        Services.AddScoped<PageGate>();
        JSInterop.Mode = JSRuntimeMode.Loose;

        this.AddAuthorization().SetAuthorized("someone");

        Services.AddSingleton<IDashboardContributor>(new TestContributor(item));

        return Render<NsDashboard>();
    }

    // IconStampedTestCard renders a span when it was handed an Icon and nothing at all when it
    // was not — the two answers this rule has to tell apart, from one component.
    static DashboardItem Mute()
    {
        return new DashboardItem { Name = "Mute", CardType = typeof(IconStampedTestCard) };
    }

    static DashboardItem Speaking()
    {
        return new DashboardItem
        {
            Name = "Speaking",
            CardType = typeof(IconStampedTestCard),
            Icon = new Glyph("<path />")
        };
    }

    [Fact]
    public void ACardThatRenderedNothingLeavesNoCellBehind()
    {
        var cut = Show(Mute());

        var slot = cut.Find(".ns-dashboard-item > .ns-dashboard-card");

        Assert.Empty(slot.Children);
        Assert.Single(cut.FindAll(Collapsed));
    }

    [Fact]
    public void ACardThatRenderedContentKeepsItsCell()
    {
        var cut = Show(Speaking());

        var slot = cut.Find(".ns-dashboard-item > .ns-dashboard-card");

        Assert.Single(slot.Children);
        Assert.Empty(cut.FindAll(Collapsed));
    }

    // The door is drawn by the card's own header now (nsail#925), so a card that rendered no
    // header rendered no door either: there is nothing left to float in an empty cell.
    [Fact]
    public void AMuteCardWithAPageTypeDrawsNoDoorAtAll()
    {
        var cut = Show(new DashboardItem
        {
            Name = "Mute",
            CardType = typeof(IconStampedTestCard),
            PageType = typeof(OpenDoorTestPage)
        });

        Assert.Empty(cut.FindAll(".mud-card-header-actions"));
        Assert.Single(cut.FindAll(Collapsed));
    }

    /// <summary>The half bUnit cannot run: the collapse is the stylesheet's, and no browser
    /// executes it here. The rule's own selector is read from the shipped ns-mud.css instead,
    /// so the markup above and the rule that acts on it cannot drift apart silently.</summary>
    [Fact]
    public void TheStylesheetCollapsesTheEmptyCell()
    {
        var css = File.ReadAllText(Path.Combine(
            RepoRoot(),
            "src/Stack/Components/NSail.Components.Mud/wwwroot/ns-mud.css"));

        Assert.Contains($"{Collapsed} {{", css, StringComparison.Ordinal);
    }

    static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NSail.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"NSail.sln was not found above {AppContext.BaseDirectory}.");
    }
}
