// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#1289: a picker is not an NsFieldBase, so the form's writability cascade —
/// the one every field reads (NsFieldBase.IsReadOnly) — reached the box it composes and
/// nothing the picker itself decides. Inside an NsForm ReadOnly the inner autocomplete drew
/// read-only on its own while the facade's ReadOnly stayed false, and everything gated on that
/// parameter went ungated: the question a quoted row still owes, the create door.
///
/// What is held here is the base's own answer (NsPickerBase.IsReadOnly / IsDisabled) and the
/// one gate the Stack owns on top of it — a lookup nobody may write to hands its field no
/// create route, which is the same sentence for the dropdown's entry and for the empty
/// picker's door, since NsMissing is drawn from that route too.</summary>
public sealed class NsPickerWritabilityTests : BunitContext, IAsyncLifetime
{
    public NsPickerWritabilityTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddComponentServices();
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddMessaging();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();

        // The route table AddRouteTable builds is the test host's, which carries none of the
        // fixture routes the create href is resolved from (NsLookupAdoptionTests' own note).
        Services.AddSingleton(new RouteTable(typeof(SelectCreatePage).Assembly, []));

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

    static bool ReadsReadOnly(IRenderedComponent<PickerFormHost> cut)
    {
        return bool.Parse(cut.Find("p.probe-readonly").TextContent);
    }

    static bool ReadsDisabled(IRenderedComponent<PickerFormHost> cut)
    {
        return bool.Parse(cut.Find("p.probe-disabled").TextContent);
    }

    static NsAutocomplete<SelectRef, Guid?> Field(IRenderedComponent<PickerFormHost> cut)
    {
        return cut.FindComponent<NsAutocomplete<SelectRef, Guid?>>().Instance;
    }

    [Fact]
    public void APickerInsideAReadOnlyFormReadsItselfReadOnly()
    {
        var cut = Render<PickerFormHost>(p => p.Add(x => x.ReadOnly, true));

        Assert.True(ReadsReadOnly(cut));
        Assert.False(ReadsDisabled(cut));
    }

    [Fact]
    public void APickerInsideADisabledFormReadsItselfDisabled()
    {
        var cut = Render<PickerFormHost>(p => p.Add(x => x.Disabled, true));

        Assert.True(ReadsDisabled(cut));
        Assert.False(ReadsReadOnly(cut));
    }

    [Fact]
    public void APickerInsideAWritableFormReadsNeither()
    {
        var cut = Render<PickerFormHost>();

        Assert.False(ReadsReadOnly(cut));
        Assert.False(ReadsDisabled(cut));
    }

    /// <summary>The door is watched OPEN first and then shut by the form alone: the create
    /// route is null until the page gate has answered, so a test that asserted the null on
    /// arrival would pass on a lookup that never read the cascade at all.</summary>
    [Fact]
    public void ALookupLosesItsCreateDoorWhenItsFormTurnsReadOnly()
    {
        var cut = Render<PickerFormHost>();

        // The gate answers in OnParametersSetAsync, which the arrival render does not wait for.
        cut.WaitForAssertion(() => Assert.NotNull(Field(cut).CreateRoute));

        cut.Render(p => p.Add(x => x.ReadOnly, true));

        Assert.Null(Field(cut).CreateRoute);
    }

    [Fact]
    public void ALookupLosesItsCreateDoorWhenItsFormTurnsDisabled()
    {
        var cut = Render<PickerFormHost>();

        cut.WaitForAssertion(() => Assert.NotNull(Field(cut).CreateRoute));

        cut.Render(p => p.Add(x => x.Disabled, true));

        Assert.Null(Field(cut).CreateRoute);
    }
}
