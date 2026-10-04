// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using AngleSharp.Dom;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Icons;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Metadata;

namespace NSail.Components.Tests;

[Route("/two-doors/appointments")]
public sealed class TwoDoorsAppointmentsProbePage : ComponentBase;

[Route("/two-doors/measures")]
public sealed class TwoDoorsMeasuresProbePage : ComponentBase;

/// <summary>nsail#1800 put the house's first two visible doors onto one page — Optical's
/// Contactología > Turnos and Mi Cuenta > Turnos, both the appointments screen — for the seat that
/// is employee and customer at once. The route and the query tie, so the two tie-breaks the drawer
/// had both answered "the first in the tree" and the counter's section opened over the door the
/// person actually touched.</summary>
public sealed class NsNavMenuTwoDoorsOnePageTests : BunitContext
{
    const string Route = "/two-doors/appointments";

    BunitNavigationManager Navigation
    {
        get { return (BunitNavigationManager)Services.GetRequiredService<NavigationManager>(); }
    }

    // The weights are the ones the drawer really carries — the counter's section ahead of Mi
    // Cuenta — and the parameter is what lets one test put Mi Cuenta first instead, so what the
    // mark follows is the open branch and not the tree order either way.
    IRenderedComponent<NsNavMenu> RenderMenu(int accountWeight = 51)
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsNavMenuTwoDoorsOnePageTests).Assembly, []));
        Services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        Services.AddScoped<PageGate>();
        Services.AddScoped<NavMenu>();
        Services.AddSingleton<Mediator>(new CapturingMediator());
        Services.AddSingleton<INavMenuContributor>(new FixedNavContributor(
            new NavMenuItem
            {
                Name = "CounterSection",
                Icon = new Glyph("event"),
                Weight = 25,
                Items =
                [
                    new NavMenuItem { Name = "CounterAppointments", PageType = typeof(TwoDoorsAppointmentsProbePage) },
                    new NavMenuItem { Name = "CounterMeasures", PageType = typeof(TwoDoorsMeasuresProbePage) }
                ]
            },
            new NavMenuItem
            {
                Name = "MyAccount",
                Icon = new Glyph("person"),
                Weight = accountWeight,
                Items =
                [
                    new NavMenuItem { Name = "MyAppointments", PageType = typeof(TwoDoorsAppointmentsProbePage) }
                ]
            }));
        JSInterop.Mode = JSRuntimeMode.Loose;

        this.AddAuthorization();

        return Render<NsNavMenu>();
    }

    static IElement Group(IRenderedComponent<NsNavMenu> cut, string name)
    {
        return cut.FindAll(".mud-nav-group").Single(group => group.GetAttribute("aria-label") == name);
    }

    // The group's own toggle and not a child's: clicking it is the one act that tells the drawer
    // which section the person is working in.
    static async Task Open(IRenderedComponent<NsNavMenu> cut, string name)
    {
        var toggle = Group(cut, name).QuerySelector("button")!;

        await cut.InvokeAsync(() => toggle.Click());
    }

    static bool IsOpen(IRenderedComponent<NsNavMenu> cut, string name)
    {
        return Group(cut, name).QuerySelector("button")!.GetAttribute("aria-expanded") == "true";
    }

    // The house's own mark, which is the styling class NsNavMenu hands the vendor for the active
    // leaf alone: the vendor's own prefix match is blind to which of two doors onto one page is
    // the live one and marks both aria-current.
    static bool IsMarked(IRenderedComponent<NsNavMenu> cut, string door)
    {
        return cut.FindAll("a.mud-nav-link")
            .Single(link => link.TextContent.Contains(door))
            .ClassList.Contains("active");
    }

    /// <summary>The defect, in both halves: the person touches Mi Cuenta > Turnos, and the
    /// counter's section is what opened over it with their own door left unmarked. The mark is
    /// read first in every test here because it is what says the navigation landed — nothing
    /// carries it before the route arrives.</summary>
    [Fact]
    public async Task TheDoorInTheOpenSectionTakesTheMarkAndKeepsTheSection()
    {
        var cut = RenderMenu();

        await Open(cut, "MyAccount");

        Navigation.NavigateTo(Route);

        cut.WaitForAssertion(() =>
        {
            Assert.True(IsMarked(cut, "MyAppointments"));
            Assert.False(IsMarked(cut, "CounterAppointments"));
            Assert.True(IsOpen(cut, "MyAccount"));
            Assert.False(IsOpen(cut, "CounterSection"));
        });
    }

    /// <summary>The mirror, with Mi Cuenta contributed ahead of the counter: what the mark follows
    /// is the open branch, in both directions, and not the order of the tree.</summary>
    [Fact]
    public async Task TheOtherSectionsDoorTakesItWhenThatIsTheOpenOne()
    {
        var cut = RenderMenu(accountWeight: 5);

        await Open(cut, "CounterSection");

        Navigation.NavigateTo(Route);

        cut.WaitForAssertion(() =>
        {
            Assert.True(IsMarked(cut, "CounterAppointments"));
            Assert.False(IsMarked(cut, "MyAppointments"));
            Assert.True(IsOpen(cut, "CounterSection"));
        });
    }

    /// <summary>Arriving on the route cold — a bookmark, a link, a reload — there is nothing to go
    /// on: no section is open and the URL cannot tell two doors onto one page apart, so the first
    /// in the tree takes it and opens.</summary>
    [Fact]
    public void ArrivingWithNoSectionOpenTakesTheFirstDoorInTheTree()
    {
        var cut = RenderMenu();

        Navigation.NavigateTo(Route);

        cut.WaitForAssertion(() =>
        {
            Assert.True(IsMarked(cut, "CounterAppointments"));
            Assert.True(IsOpen(cut, "CounterSection"));
        });
    }
}
