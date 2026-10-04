// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#579 — Leonardo's screenshot caught the pager row (page-size select, counter,
/// arrows) clipped under the end of the list. Three E2E rounds chasing the exact viewport/timing
/// window that reproduces it each turned up a test that passed identically with or without the
/// fix (no browser geometry survives that chase without flaking) — the mechanism itself needs no
/// browser to pin: the vendor's own .mud-table-pagination carries overflow: auto, which gives a
/// flex item automatic minimum size zero in that axis (CSS Flexbox 4.5), so whenever NsTable's
/// box is shorter than its rows plus its pager, the shrink lands on the pager too. What is pinned
/// here, the NsDashboardCardStretchTests way: the selector the rule depends on reaches real DOM
/// under Page mode with real rows (bUnit needs no viewport for that — ScrollTableHost forces
/// Mode explicitly), and the rule that neutralizes the shrink shipped in the stylesheet, read
/// verbatim so the two cannot drift apart silently.</summary>
public sealed class NsTablePagerShrinkTests : BunitContext, IAsyncLifetime
{
    public NsTablePagerShrinkTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;
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
    public void ThePagerRowIsWhatTheShrinkRuleReaches()
    {
        var cut = Render<ScrollTableHost>(parameters => parameters.Add(p => p.Mode, NsTableMode.Page));

        // Real rows, real pager, no viewport needed — Mode="Page" takes Auto's own resolution
        // out of the question, so this is the exact DOM the CSS rule below has to reach.
        Assert.NotEmpty(cut.FindAll(".ns-table .mud-table-body tr"));
        Assert.NotEmpty(cut.FindAll(".ns-table .mud-table-pagination"));
    }

    [Fact]
    public void TheStylesheetTakesThePagerOutOfTheShrinkDistribution()
    {
        var css = ReadStylesheet();

        var block = Rule(css, ".ns-table .mud-table-pagination {");

        Assert.Contains("flex-shrink: 0;", block);
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
