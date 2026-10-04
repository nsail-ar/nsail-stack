// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#857: Crear… from a lookup saved, bound and then said nothing — the create
/// surface stayed open on top of the screen that asked for it, the field stayed blank, and the
/// next search did not see the row until a reload. The round trip is the contract, all of it:
/// the create surface goes, the field reads the row, and the lookup's next search sees it.
///
/// The chain is the reported one, rung for rung: a tracked form in the ASIDE whose lookup's
/// create entry escalates to the MODAL (Surfaces.Auto), a create page that publishes inside its
/// own submit and then navigates in place, and an :after on the lookup's binding that sends
/// while that submit is still running.
///
/// The lookup's search is slow here on purpose. Zero latency is the state no slot is ever in,
/// and with the search already back before the entry is clicked, none of the three failures can
/// happen at all — the seam is the OVERLAP of the field's two reads (NsAutocompleteResolveTests
/// isolates each order).</summary>
public sealed class NsLookupCreateRoundTripTests : BunitContext, IAsyncLifetime
{
    readonly SelectStore _store = new() { Latency = TimeSpan.FromMilliseconds(300) };

    public NsLookupCreateRoundTripTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddComponentServices();
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddMessaging();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton<IStringSource>(new FixedStrings(new Dictionary<string, string>
        {
            [new MetadataProvider().KeyFor(typeof(SelectRef))] = "Talleres",
            ["Common.CreateNew"] = "Crear {0}…",
        }));
        Services.AddSingleton<StringCatalog>();
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(_store);

        // The route table AddRouteTable builds is the test host's, which carries none of the
        // fixture routes the create href is resolved from (NsLookupAdoptionTests' own note).
        Services.AddSingleton(new RouteTable(typeof(LookupRoundTripPage).Assembly, []));

        AddAuthorization().SetAuthorized("probe");
    }

    // MudPopoverProvider resolves a MudBlazor service that is IAsyncDisposable-only, so bUnit's
    // synchronous teardown cannot dispose it (NsLookupCreateEntryTests' own note).
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
        Navigation.NavigateTo("/probe?aside=probe%2Flookup%2Faside");

        return Render<NsRouter>(p => p
            .Add(x => x.AppAssembly, typeof(LookupRoundTripPage).Assembly)
            .Add(x => x.AdditionalAssemblies, [])
            .Add(x => x.DefaultLayout, typeof(ProbeLayout)));
    }

    static NsAutocomplete<SelectRef, Guid?> Field(IRenderedComponent<NsRouter> app)
    {
        return app.FindComponent<NsAutocomplete<SelectRef, Guid?>>().Instance;
    }

    // What the entry's own link resolves: the raw create route and the target, against the
    // surface the field stands on — the aside, so Auto escalates to the modal.
    static string CreateHref(IRenderedComponent<NsRouter> app)
    {
        var field = Field(app);

        return field.Surface!.GetHref(field.CreateRoute!, field.CreateTarget);
    }

    // The gesture, not a shortcut: the dropdown is opened, the create entry inside it is
    // clicked while the search it started is still out, and the create page's own form is
    // submitted in the modal that entry escalated to. The opening is deliberately not awaited —
    // its own read is what everything below overlaps with — and is spent at the foot.
    async Task<IRenderedComponent<NsRouter>> CreateFromTheLookup()
    {
        var app = RenderApp();

        var opening = app.Find("input.mud-input-root").FocusAsync(new FocusEventArgs());

        app.WaitForElement(".ns-lookup-create a");

        await app.InvokeAsync(() => app.Find(".ns-lookup-create a").Click());

        app.WaitForElement(".probe-create form");

        await app.InvokeAsync(() => app.Find(".probe-create form").Submit());

        await opening;

        return app;
    }

    [Fact]
    public void TheCreateEntryEscalatesFromTheAsideToTheModal()
    {
        var app = RenderApp();

        Assert.Contains("modal=", CreateHref(app), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheSaveBindsToTheFieldThatAskedForIt()
    {
        var app = await CreateFromTheLookup();

        app.WaitForAssertion(() =>
            Assert.Equal(SelectCreatePage.Row.ToString(), app.Find("p.probe-picked").TextContent));
    }

    [Fact]
    public async Task SavingFromAnInlineCreateClosesItsPanel()
    {
        var app = await CreateFromTheLookup();

        app.WaitForAssertion(() =>
            Assert.DoesNotContain("modal=", Navigation.Uri, StringComparison.Ordinal));

        Assert.Contains("aside=", Navigation.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheLookupShowsTheCreatedRow()
    {
        var app = await CreateFromTheLookup();

        app.WaitForAssertion(() =>
            Assert.Equal(SelectCreatePage.RowName, app.Find("input.mud-input-root").GetAttribute("value")));
    }

    /// <summary>The report's own next gesture: the user goes back to the field to check, or to
    /// type the name they just made. That opens the list, which searches — and the answer to
    /// "what does the value I am holding read as" is still out at that moment on any install
    /// whose reads cost a round trip.</summary>
    [Fact]
    public async Task TheLookupStillShowsTheCreatedRowWhenTheUserGoesBackToTheField()
    {
        var app = await CreateFromTheLookup();

        await app.Find("input.mud-input-root").FocusAsync(new FocusEventArgs());

        app.WaitForAssertion(() =>
            Assert.Equal(SelectCreatePage.RowName, app.Find("input.mud-input-root").GetAttribute("value")));
    }

    /// <summary>nsail#1569: the field reads the created row (TheLookupShowsTheCreatedRow,
    /// above), but the aside form's own EditContext never learned the model changed — the
    /// adoption sets Value through the lookup's raw ValueChanged, not through
    /// NsFieldBase.SetValue, which is the only path that calls NotifyFieldChanged. Guardar
    /// reads Surface.HasChanges, and nothing ever set it.</summary>
    [Fact]
    public async Task TheRoundTripEnablesTheAsideFormsOwnSubmit()
    {
        var app = await CreateFromTheLookup();

        app.WaitForAssertion(() =>
        {
            Assert.True(Field(app).Surface!.HasChanges);
            Assert.False(app.Find("button[type=submit]").HasAttribute("disabled"));
        });
    }

    [Fact]
    public async Task TheNextSearchSeesTheRowWithoutAReload()
    {
        var app = await CreateFromTheLookup();

        app.WaitForAssertion(() =>
            Assert.DoesNotContain("modal=", Navigation.Uri, StringComparison.Ordinal));

        await app.Find("input.mud-input-root").FocusAsync(new FocusEventArgs());

        app.WaitForAssertion(() =>
            Assert.Contains(app.FindAll(".mud-list-item"),
                item => item.TextContent.Contains(SelectCreatePage.RowName, StringComparison.Ordinal)));
    }
}
