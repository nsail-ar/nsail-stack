// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Metadata;

namespace NSail.Components.Tests;

// The two grants the story turns on: a read every signed-in person holds about themselves, and
// one only a seat that works the shop holds.
public sealed class OwnRead;

public sealed class CounterRead;

[Route("/permission/appointments")]
[Authorize<OwnRead>]
public sealed class PermittedAppointmentsProbePage : ComponentBase;

[Route("/permission/measures")]
[Authorize<OwnRead>]
public sealed class PermittedMeasuresProbePage : ComponentBase;

[Route("/permission/counter")]
[Authorize<CounterRead>]
public sealed class PermittedCounterProbePage : ComponentBase;

/// <summary>nsail#1800: a portal customer read the counter's drawer. Every page under the shop's
/// sections is a read the portal legitimately holds about itself, so the destination page's gate
/// answers the same for both — and a group is never asked about itself at all, so it survived
/// whichever child passed. An entry may therefore name a permission of its own, ANDed with the
/// page's and never instead of it.</summary>
public sealed class NsNavMenuEntryPermissionTests : BunitContext
{
    IRenderedComponent<NsNavMenu> RenderMenu()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsNavMenuEntryPermissionTests).Assembly, []));
        Services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        Services.AddScoped<PageGate>();
        Services.AddScoped<NavMenu>();
        Services.AddSingleton<Mediator>(new CapturingMediator());
        Services.AddSingleton<INavMenuContributor>(new FixedNavContributor(
            // The shop's section: a group the session may not have, every child of which its own
            // page would let it open.
            new NavMenuItem
            {
                Name = "CounterSection",
                Permission = typeof(CounterRead),
                Items =
                [
                    new NavMenuItem { Name = "CounterAppointments", PageType = typeof(PermittedAppointmentsProbePage) },
                    new NavMenuItem { Name = "CounterMeasures", PageType = typeof(PermittedMeasuresProbePage) }
                ]
            },
            // The person's own door onto the same page as the section's first child.
            new NavMenuItem { Name = "OwnAppointments", PageType = typeof(PermittedAppointmentsProbePage) },
            // A permission the session holds over a page it may not open: the AND, from the
            // other side.
            new NavMenuItem
            {
                Name = "PermittedButClosed",
                PageType = typeof(PermittedCounterProbePage),
                Permission = typeof(OwnRead)
            }));
        JSInterop.Mode = JSRuntimeMode.Loose;

        // The real fabrication path — bUnit's own authorization doubles answer from a context the
        // test sets and never reach a policy provider, and the provider IS what turns a message
        // type into the requirement here (testing.md). Its placeholders are registered before
        // this method and AddAuthorizationCore only ever TryAdds, so they are removed.
        Services.RemoveAll<IAuthorizationService>();
        Services.RemoveAll<IAuthorizationPolicyProvider>();
        Services.RemoveAll<AuthenticationStateProvider>();
        Services.AddAuthorizationCore();
        Services.AddSingleton<IAuthorizationPolicyProvider, MessagePolicyProvider>();
        Services.AddSingleton<IAuthorizationHandler>(new GrantedMessages(typeof(OwnRead)));
        Services.AddSingleton<AuthenticationStateProvider>(new ProbeAuthenticationStateProvider());

        return Render<NsNavMenu>();
    }

    /// <summary>The defect itself: the section goes, though the gate on every page under it says
    /// yes. Nothing else in the drawer could have hidden it — a group is not asked about
    /// itself.</summary>
    [Fact]
    public void AGroupWhosePermissionTheSessionLacksIsNotDrawn()
    {
        Assert.DoesNotContain("CounterSection", RenderMenu().Markup);
    }

    [Fact]
    public void TheBranchUnderItGoesWithIt()
    {
        var markup = RenderMenu().Markup;

        Assert.DoesNotContain("CounterAppointments", markup);
        Assert.DoesNotContain("CounterMeasures", markup);
    }

    /// <summary>The other half of the same claim, and the case the page gate cannot answer at
    /// all: two doors onto one page, where only the door differs.</summary>
    [Fact]
    public void TheUngatedDoorOntoTheSamePageStays()
    {
        Assert.Contains("OwnAppointments", RenderMenu().Markup);
    }

    /// <summary>ANDed, never a replacement: a permission the session holds does not open a door
    /// whose page refuses it.</summary>
    [Fact]
    public void APermissionTheSessionHoldsDoesNotOverrideThePagesOwnGate()
    {
        Assert.DoesNotContain("PermittedButClosed", RenderMenu().Markup);
    }

    /// <summary>The gate takes the door and not the tree, like every other hiding: the pages
    /// under it keep the glyph on their own title bars.</summary>
    [Fact]
    public void TheGatedBranchsPageStillHasAMenuEntryToDeriveAGlyphFrom()
    {
        var cut = RenderMenu();
        var menu = cut.Services.GetRequiredService<NavMenu>();

        var items = menu.GetItems(null).GetAwaiter().GetResult();

        Assert.NotNull(NavMenuItem.Find(items, typeof(PermittedMeasuresProbePage)));
    }
}
