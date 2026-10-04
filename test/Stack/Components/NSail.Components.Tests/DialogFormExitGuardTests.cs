// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>The hosted rung of the same law UnsavedChangesGuardTests pins for named surfaces:
/// a form inside a DIALOG is asked before it is left, and Cancelar has to mean cancelled.
/// SurfaceContext.Follow used to navigate and close on the next line, with nothing between
/// them — but the guard is a NavigationLock handler that AWAITS the question, so the dialog
/// was already torn down by the time the person read it. Declining then kept the address (the
/// guard did its half) and lost the document anyway (the close had already happened), which is
/// the worst of both answers.</summary>
public sealed class DialogFormExitGuardTests : BunitContext, IAsyncLifetime
{
    readonly CountingDialogManager _dialogs = new();
    int _closes;

    public DialogFormExitGuardTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddComponentServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton<Mediator>(new CapturingMediator());
        Services.AddSingleton(BuildRouteTable());
        Services.AddScoped<DialogManager>(_ => _dialogs);
    }

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    static RouteTable BuildRouteTable()
    {
        return new(typeof(DialogFormExitGuardTests).Assembly, Array.Empty<System.Reflection.Assembly>());
    }

    BunitNavigationManager Navigation
    {
        get { return (BunitNavigationManager)Services.GetRequiredService<NavigationManager>(); }
    }

    // Target="Auto" from a host-managed surface escalates to the aside (NsLinkHistoryTests
    // pins that resolution), which keeps the path and so is a link NsLink drives itself —
    // Follow, the seam under test, rather than an anchor Blazor takes over.
    const string Destination = "optical/prescriptions/new";
    const string OpenedAside = "aside=optical%2Fprescriptions%2Fnew";

    IRenderedComponent<DialogFormLinkHost> RenderDialog()
    {
        Navigation.NavigateTo("/optical/work-orders/new");

        return Render<DialogFormLinkHost>(p => p
            .Add(x => x.RouteTable, BuildRouteTable())
            .Add(x => x.Model, new CheckBoxTrackingModel())
            .Add(x => x.Close, () => _closes++)
            .Add(x => x.Href, Destination));
    }

    async Task<IRenderedComponent<DialogFormLinkHost>> RenderDirtyDialog()
    {
        var host = RenderDialog();

        // A real edit through a real DOM event, so the surface is holding a document the way
        // a person makes it hold one.
        await host.InvokeAsync(() => host.Find("input[type=checkbox]").Change(true));

        Assert.True(host.Instance.Surface!.HasChanges);

        return host;
    }

    string Address
    {
        get { return Navigation.Uri[Navigation.BaseUri.TrimEnd('/').Length..]; }
    }

    /// <summary>The defect, in one row: Cancelar keeps the dialog AND keeps the page where it
    /// stands. Before the fix the close counter read 1 here — the person was asked, said no,
    /// and watched the form vanish regardless.</summary>
    [Fact]
    public async Task DecliningTheProtestInADialog_ClosesNothingAndMovesNothing()
    {
        _dialogs.Answer = false;

        var host = await RenderDirtyDialog();

        await host.InvokeAsync(() => host.Find("a").Click());

        Assert.Equal(1, _dialogs.Confirms);
        Assert.Equal(0, _closes);
        Assert.Equal("/optical/work-orders/new", Address);
    }

    /// <summary>The other half, and the reason the close cannot simply be deleted: accepting
    /// still takes the person to the destination and still takes the dialog off the top of it,
    /// exactly once.</summary>
    [Fact]
    public async Task AcceptingTheProtestInADialog_ClosesItOnceAndFollowsTheLink()
    {
        var host = await RenderDirtyDialog();

        await host.InvokeAsync(() => host.Find("a").Click());

        Assert.Equal(1, _dialogs.Confirms);
        Assert.Equal(1, _closes);
        Assert.Contains(OpenedAside, Address, StringComparison.Ordinal);
    }

    /// <summary>And a dialog holding nothing is not questioned at all — the guard is about a
    /// document, not about being in a dialog. This is the path every dialog-hosted link that
    /// is not a form takes, so it is what proves the close still fires at all.</summary>
    [Fact]
    public async Task FollowingALinkOutOfACleanDialog_AsksNothingAndStillCloses()
    {
        var host = RenderDialog();

        await host.InvokeAsync(() => host.Find("a").Click());

        Assert.Equal(0, _dialogs.Confirms);
        Assert.Equal(1, _closes);
        Assert.Contains(OpenedAside, Address, StringComparison.Ordinal);
    }
}
