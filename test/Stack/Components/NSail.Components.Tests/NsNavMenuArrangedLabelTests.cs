// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Metadata;
using NSail.Settings;

namespace NSail.Components.Tests;

[Route("/arranged/label")]
public sealed class ArrangedLabelProbePage : ComponentBase;

[Route("/arranged/denied")]
[Microsoft.AspNetCore.Authorization.Authorize(Roles = "admin")]
public sealed class ArrangedDeniedProbePage : ComponentBase;

/// <summary>The install's own word, in the drawer. A module's entry is drawn with the
/// NavMenu.{Name} string as it always was; a row the shop renamed is drawn with what the shop
/// typed, and the search over the drawer reads the same word — one answer for the label, so a
/// renamed door can still be found by the name the reader can see.</summary>
public sealed class NsNavMenuArrangedLabelTests : BunitContext
{
    IRenderedComponent<NsNavMenu> RenderMenu()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog(
            [new TwoLanguageStrings("NavMenu.Scheduling", "Scheduling", "Agenda")]));
        Services.AddSingleton<LanguageProvider>(new LanguageProvider { Current = "es" });
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsNavMenuArrangedLabelTests).Assembly, []));
        Services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        Services.AddScoped<PageGate>();
        Services.AddScoped<NavMenu>();
        Services.AddSingleton<Mediator>(new CapturingMediator());
        Services.AddSingleton<INavMenuContributor>(new FixedNavContributor(
            new NavMenuItem { Name = "Scheduling", PageType = typeof(ArrangedLabelProbePage) },
            new NavMenuItem { Name = "Denied", PageType = typeof(ArrangedDeniedProbePage) }));
        Services.AddSingleton<INavMenuArrangement>(new FixedNavArrangement(
            new NavMenuArrangementEntry
            {
                Name = "Scheduling",
                Labels = new Dictionary<string, string> { ["es"] = "Contactología" },
            },
            new NavMenuArrangementEntry
            {
                Name = "Denied",
                Order = 1,
                Shown = true,
                Labels = new Dictionary<string, string> { ["es"] = "Caja" },
            }));
        JSInterop.Mode = JSRuntimeMode.Loose;

        // A session with no role: the probe page below carries an [Authorize(Roles = "admin")]
        // this reader does not satisfy.
        this.AddAuthorization().SetAuthorized("arranged-probe");

        return Render<NsNavMenu>();
    }

    [Fact]
    public void TheDrawerDrawsTheWordTheInstallStored()
    {
        var markup = RenderMenu().Markup;

        Assert.Contains("Contactología", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Agenda", markup, StringComparison.Ordinal);
    }

    /// <summary>The search runs over the same GetName the row is drawn with, so what the reader
    /// types is what the reader sees — and the catalog's word, which is nowhere on the screen
    /// any more, finds nothing. NsSearchField debounces, so the assertion waits for the filter
    /// rather than reading the DOM straight after the keystroke (NsNavMenuSeparatorTests carries
    /// the same note).</summary>
    [Theory]
    [InlineData("contacto", true)]
    [InlineData("agenda", false)]
    public async Task TheSearchReadsTheWordTheDrawerDraws(string typed, bool found)
    {
        var cut = RenderMenu();

        await cut.InvokeAsync(() => cut.Find(".mud-input-slot").Input(typed));

        cut.WaitForAssertion(
            () => Assert.Equal(found, cut.FindAll("a[href='/arranged/label']").Count > 0),
            TimeSpan.FromSeconds(10));
    }

    /// <summary>Hiding is not a permission, and neither is showing: an arrangement that orders,
    /// renames and shows an entry whose page this reader may not open draws nothing at all. The
    /// gate is the page's own authorize attributes, asked after the arrangement like any other
    /// contribution — an install arranges what is already allowed.</summary>
    [Fact]
    public void AnArrangedEntryStillAnswersToThePagesOwnGate()
    {
        var markup = RenderMenu().Markup;

        // The allowed entry beside it, from the same arrangement and the same render: without
        // it this would pass just as well on a drawer that drew nothing.
        Assert.Contains("Contactología", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Caja", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("/arranged/denied", markup, StringComparison.Ordinal);
    }
}
