// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#389 (Leonardo, "se carga el médico como paciente"): every lookup of a
/// concept subscribed to that concept's saved event with no owner, so creating a médico from
/// a receta's lookup wrote the new row into the OT's Paciente as well. A save now names who
/// asked for it — the lookup mints a token, carries it in its create href, the create page
/// holds it from the query and its Publish wears it — and only that lookup adopts.
///
/// The whole circuit, live, with the real Mediator (SubscribeRerenderTests' precedent): two
/// lookups of one concept on one screen, the create page opened from ONE of them in the aside
/// while the screen stays mounted underneath, and the save published from a real NsForm submit
/// by a page that writes nothing about correlation. The form matters: a successful submit in an
/// overlay CLOSES it, so the publish only wears the token while NsForm still runs the handler
/// before the close.</summary>
public sealed class NsLookupAdoptionTests : BunitContext, IAsyncLifetime
{
    public NsLookupAdoptionTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddComponentServices();

        // The dropdown is never opened here, so its popover provider is never mounted — the
        // vendor's check for one would fail on the closed field alone.
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddMessaging();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();

        // The route table AddRouteTable builds is the test host's, which carries none of the
        // fixture routes the create href is resolved from (SubscribeRerenderTests' own note).
        Services.AddSingleton(new RouteTable(typeof(SelectLookupPage).Assembly, []));

        AddAuthorization().SetAuthorized("probe");
    }

    // MudPopoverProvider resolves a MudBlazor service that is IAsyncDisposable-only, so
    // bUnit's synchronous teardown cannot dispose it (NsLookupCreateEntryTests' own note).
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    BunitNavigationManager Navigation
    {
        get { return (BunitNavigationManager)Services.GetRequiredService<NavigationManager>(); }
    }

    IRenderedComponent<NsRouter> RenderApp()
    {
        Navigation.NavigateTo("/probe/lookup");

        return Render<NsRouter>(p => p
            .Add(x => x.AppAssembly, typeof(SelectLookupPage).Assembly)
            .Add(x => x.AdditionalAssemblies, [])
            .Add(x => x.DefaultLayout, typeof(ProbeLayout)));
    }

    // The create entry lives at the bottom of a vendor popover, which only exists while the
    // dropdown is open; what the lookup hands the autocomplete is the create page's raw route
    // and the target, which the entry's own link resolves against the surface it stands on —
    // the main one here, so the root surface answers exactly as that link would. Following the
    // result is the click (SubscribeRerenderTests opens its own aside the same way). Where the
    // entry renders and how it looks are NsLookupCreateEntryTests'.
    string CreateHrefOf(IRenderedComponent<NsRouter> app, int index)
    {
        var field = app.FindComponents<NsAutocomplete<SelectRef, Guid?>>()[index].Instance;

        return Services.GetRequiredService<RootSurface>().Surface.GetHref(field.CreateRoute!, field.CreateTarget);
    }

    static string First(IRenderedComponent<NsRouter> app)
    {
        return app.Find("p.probe-first").TextContent;
    }

    static string Second(IRenderedComponent<NsRouter> app)
    {
        return app.Find("p.probe-second").TextContent;
    }

    [Fact]
    public void TwoLookupsOfOneConceptAskWithTokensOfTheirOwn()
    {
        var app = RenderApp();

        Assert.NotEqual(CreateHrefOf(app, 0), CreateHrefOf(app, 1));
    }

    [Fact]
    public async Task OnlyTheLookupThatOpenedTheCreateAdoptsWhatItSaved()
    {
        var app = RenderApp();

        Navigation.NavigateTo(CreateHrefOf(app, 0));

        await app.InvokeAsync(() => app.Find("form").Submit());

        Assert.Equal(SelectCreatePage.Row.ToString(), First(app));
        Assert.Equal(string.Empty, Second(app));
    }

    [Fact]
    public async Task TheOtherLookupAdoptsWhenTheCreateWasOpenedFromIt()
    {
        var app = RenderApp();

        Navigation.NavigateTo(CreateHrefOf(app, 1));

        await app.InvokeAsync(() => app.Find("form").Submit());

        Assert.Equal(string.Empty, First(app));
        Assert.Equal(SelectCreatePage.Row.ToString(), Second(app));
    }

    /// <summary>The deliberate behavior change: a save nobody asked for — the concept's own
    /// list page creating a row, a handler's event — is adopted by no lookup at all, where
    /// before it was adopted by every one of them.</summary>
    [Fact]
    public async Task ASaveWithNoTokenIsAdoptedByNobody()
    {
        var app = RenderApp();

        Navigation.NavigateTo("/probe/lookup?aside=probe%2Flookup%2Fcreate");

        await app.InvokeAsync(() => app.Find("form").Submit());

        Assert.Equal(string.Empty, First(app));
        Assert.Equal(string.Empty, Second(app));
    }

}
