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

/// <summary>Leonardo, 2026-08-11 screenshot: "¿se puede hacer que la card se estire a la más
/// alta de la fila?" — the grid already stretches the ITEM (CSS Grid's own block-axis
/// align-items: stretch), what stayed short was the CARD inside it, a plain block that took
/// only its own content height and left the difference as air below. The cure is CSS layout
/// (.ns-dashboard-item turns flex-column, the card fills the main axis) that neither bUnit nor
/// any assertion here can lay out or measure — no browser runs under this suite. What IS
/// pinned: the DOM shape the rule depends on (the card's rendered root sits exactly where the
/// selector reaches, "> .ns-dashboard-card > *", the same reach the mute-cell rule already
/// uses) and that the rule shipped in the stylesheet, read verbatim so the two cannot drift
/// apart silently. The pixels are the Architect probe's question.</summary>
public sealed class NsDashboardCardStretchTests : BunitContext, IAsyncLifetime
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

    [Fact]
    public void TheCardsRenderedRootIsWhatTheStretchRuleReaches()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsDashboardCardStretchTests).Assembly, []));
        Services.AddScoped<RootSurface>();
        Services.AddScoped<SurfaceHistory>();
        Services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        Services.AddScoped<PageGate>();
        JSInterop.Mode = JSRuntimeMode.Loose;

        this.AddAuthorization().SetAuthorized("someone");

        Services.AddSingleton<IDashboardContributor>(new TestContributor(new DashboardItem
        {
            Name = "Speaking",
            CardType = typeof(IconStampedTestCard),
            Icon = new Glyph("<path />")
        }));

        var cut = Render<NsDashboard>();

        // The selector the stylesheet hands the spare height to, restated here so the DOM and
        // the rule are read against the same question rather than two that drifted apart.
        var reached = cut.Find(".ns-dashboard-item > .ns-dashboard-card > *");

        Assert.Equal("test-card-icon", reached.ClassName);
    }

    [Fact]
    public void TheStylesheetTurnsTheItemIntoAFlexColumn()
    {
        var css = ReadStylesheet();

        var block = Rule(css, ".ns-dashboard-item {");

        Assert.Contains("display: flex;", block);
        Assert.Contains("flex-direction: column;", block);
    }

    [Fact]
    public void TheStylesheetHandsTheCardTheSpareHeight()
    {
        var css = ReadStylesheet();

        var block = Rule(css, ".ns-dashboard-item > .ns-dashboard-card > * {");

        Assert.Contains("flex: 1 1 auto;", block);
    }

    // A muted card renders no child under the slot, so the reaching selector above matches
    // nothing for it — the collapse rule that hides the whole cell is untouched by this story
    // and stays pinned on its own (NsDashboardMuteCardTests).
    [Fact]
    public void TheCollapseRuleForAMutedCardIsUnchanged()
    {
        var css = ReadStylesheet();

        Assert.Contains(".ns-dashboard-item:not(:has(> .ns-dashboard-card > *)) {", css, StringComparison.Ordinal);
    }

    static string Rule(string css, string selector)
    {
        var start = css.IndexOf(selector, StringComparison.Ordinal);
        Assert.True(start >= 0, $"'{selector}' was not found in the stylesheet.");

        var end = css.IndexOf('}', start);
        Assert.True(end >= 0, $"'{selector}' has no closing brace.");

        return css[start..end];
    }

    static string ReadStylesheet()
    {
        return File.ReadAllText(Path.Combine(
            RepoRoot(),
            "src/Stack/Components/NSail.Components.Mud/wwwroot/ns-mud.css"));
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
