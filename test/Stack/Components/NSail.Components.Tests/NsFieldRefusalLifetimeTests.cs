// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#1891: a refusal a field raised for itself must not outlive the field. The
/// field's own ValidationMessageStore stays registered in the EditContext's field state after
/// the field lets go of it, and Validate() counts the messages of every store there — so a
/// field that left the screen refused held every later submit, and NsForm's gate returns mute.
/// The person sees a clean, complete form and a dead button.</summary>
public sealed class NsFieldRefusalLifetimeTests : BunitContext, IAsyncLifetime
{
    public NsFieldRefusalLifetimeTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // The MudPopoverProvider teardown rule NsFieldRequiredLiftTests carries: bunit's synchronous
    // Dispose cannot tear down MudBlazor's popover provider, so xunit's async lifecycle takes over.
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    IRenderedComponent<RequiredFieldsHost> Mount(RequiredFieldsModel model, Action? submitted = null)
    {
        return Render<RequiredFieldsHost>(p => p
            .Add(x => x.Model, model)
            .Add(x => x.Submitted, () => submitted?.Invoke()));
    }

    static Task Save(IRenderedComponent<RequiredFieldsHost> cut)
    {
        return cut.InvokeAsync(() => cut.Find("form").Submit());
    }

    static void Hide(IRenderedComponent<RequiredFieldsHost> cut, bool hidden)
    {
        cut.Render(p => p.Add(x => x.Hidden, hidden));
    }

    static IRenderedComponent<NsSelect<SelectRef, Guid>> Select(IRenderedComponent<RequiredFieldsHost> cut)
    {
        return cut.FindComponent<NsSelect<SelectRef, Guid>>();
    }

    static IRenderedComponent<NsAutocomplete<SelectRef, Guid>> Lookup(IRenderedComponent<RequiredFieldsHost> cut)
    {
        return cut.FindComponent<NsAutocomplete<SelectRef, Guid>>();
    }

    static void Refuses(IRenderedComponent<IComponent> field)
    {
        Assert.Contains("Problems.Required", field.Markup);
        Assert.NotEmpty(field.FindAll(".mud-input-error"));
    }

    static void Accepts(IRenderedComponent<IComponent> field)
    {
        Assert.DoesNotContain("Problems.Required", field.Markup);
        Assert.Empty(field.FindAll(".mud-input-error"));
    }

    // The select is the probe because its refusal is the FIELD's own: PatientId is a Guid with
    // no annotation that can refuse it, so nothing but the field's store holds the submit. Name
    // and CategoryId are answered to keep the declared half and the other field quiet.
    static RequiredFieldsModel OnlyTheSelectEmpty()
    {
        return new RequiredFieldsModel { Name = "Ok", CategoryId = RequiredFieldsHost.Option.Id };
    }

    /// <summary>The defect: "Soy yo" ticked after a refused Save, and Guardar does nothing and
    /// says nothing from then on.</summary>
    [Fact]
    public async Task AFieldThatLeavesTheScreenRefused_NoLongerHoldsTheSubmit()
    {
        var submitted = 0;
        var cut = Mount(OnlyTheSelectEmpty(), () => submitted++);

        await Save(cut);

        Assert.Equal(0, submitted);
        Refuses(Select(cut));

        Hide(cut, hidden: true);

        await Save(cut);

        Assert.Equal(1, submitted);
    }

    /// <summary>And it comes back clean: a field back on the screen owes the form nothing until
    /// the next Save, so what it would draw is whatever the orphaned store left behind.</summary>
    [Fact]
    public async Task AFieldBackOnTheScreen_DrawsNoRefusal()
    {
        var cut = Mount(OnlyTheSelectEmpty());

        await Save(cut);

        Refuses(Select(cut));

        Hide(cut, hidden: true);
        Hide(cut, hidden: false);

        Accepts(Select(cut));
    }

    /// <summary>The clear does not weaken the gate: only the field that left takes its refusal
    /// with it, and a field still on the screen holding one still refuses the save.</summary>
    [Fact]
    public async Task AFieldStillOnTheScreen_StillRefusesTheSubmit()
    {
        var submitted = 0;
        var cut = Mount(new RequiredFieldsModel { Name = "Ok" }, () => submitted++);

        await Save(cut);

        Refuses(Select(cut));
        Refuses(Lookup(cut));

        Hide(cut, hidden: true);

        await Save(cut);

        Assert.Equal(0, submitted);
        Refuses(Lookup(cut));
    }
}
