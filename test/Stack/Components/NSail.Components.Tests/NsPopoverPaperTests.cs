// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>The sibling of the dialog's own dead compound (b9c2a662: ".mud-dialog.mud-paper
/// names a compound MudBlazor does not render at all" — its dialog paints Surface off
/// .mud-dialog alone). ns-mud.css left ".mud-popover.mud-paper" standing, unmeasured, in the
/// same rule that compound's fixed sibling now shares with .mud-dialog. Rendering an
/// NsAutocomplete's popover open (the same OpenOnFocus state NsLookupCreateMenuClosesTests
/// already proves) and reading its own class list off the DOM settles it without a browser —
/// MudBlazor 9.10 DOES put .mud-paper on the popover, so the selector is live and the raised
/// paint it carries actually reaches the menu.</summary>
public sealed class NsPopoverPaperTests : BunitContext, IAsyncLifetime
{
    public NsPopoverPaperTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsPopoverPaperTests).Assembly, []));
        Services.AddScoped<RootSurface>();
        Services.AddScoped<SurfaceHistory>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // MudPopoverProvider resolves a MudBlazor service that is IAsyncDisposable-only and
    // internal, so bUnit's synchronous teardown cannot dispose it (NsLookupCreateEntryTests'
    // own note, same fixture family).
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    static readonly SelectRef[] Two =
    [
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alpha"),
        new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Beta"),
    ];

    [Fact]
    public async Task AnOpenAutocompletePopover_CarriesMudPaper()
    {
        var cut = Render<LookupCreateHost>(p => p.Add(x => x.Items, Two));

        await cut.Find("input").FocusAsync(new FocusEventArgs());
        cut.Render();

        var popover = cut.Find(".mud-popover-open");

        // If this ever fails, MudBlazor stopped putting .mud-paper on the popover the way it
        // already stopped on the dialog (b9c2a662) — the fix is the same shape: drop
        // .mud-paper from ".mud-popover.mud-paper" in ns-mud.css so the raised paint keeps
        // reaching the menu instead of matching nothing.
        Assert.Contains("mud-paper", popover.ClassList);
    }
}
