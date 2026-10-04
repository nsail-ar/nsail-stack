// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#1868: the other half of the Required parameter's own check. The refusal a
/// submit raises is posted to the EditContext and nothing used to take it back — a pick travels
/// SetValue, which tells the form the field changed and never re-asks the field's own store — so
/// "Obligatorio" stood under a field already holding a valid answer until a second submit. The
/// value arriving is what lifts it, and only for a form that has already asked: a refusal is
/// drawn at Save, never at the keystroke.</summary>
public sealed class NsFieldRequiredLiftTests : BunitContext, IAsyncLifetime
{
    public NsFieldRequiredLiftTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // The MudPopoverProvider teardown rule NsFormRequiredTests carries: bunit's synchronous
    // Dispose cannot tear down MudBlazor's popover provider, so xunit's async lifecycle takes over.
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    IRenderedComponent<RequiredFieldsHost> Mount(RequiredFieldsModel model, Action? submitted = null, bool clearable = false)
    {
        return Render<RequiredFieldsHost>(p => p
            .Add(x => x.Model, model)
            .Add(x => x.Clearable, clearable)
            .Add(x => x.Submitted, () => submitted?.Invoke()));
    }

    static Task Save(IRenderedComponent<RequiredFieldsHost> cut)
    {
        return cut.InvokeAsync(() => cut.Find("form").Submit());
    }

    static IRenderedComponent<NsSelect<SelectRef, Guid>> Select(IRenderedComponent<RequiredFieldsHost> cut)
    {
        return cut.FindComponent<NsSelect<SelectRef, Guid>>();
    }

    static IRenderedComponent<NsAutocomplete<SelectRef, Guid>> Lookup(IRenderedComponent<RequiredFieldsHost> cut)
    {
        return cut.FindComponent<NsAutocomplete<SelectRef, Guid>>();
    }

    // The real gesture, not a value written behind the vendor's back (testing.md): the closed
    // select is opened by its own gesture and the option is the node a finger lands on. The
    // vendor opens the list on mousedown rather than click — a press is already the open.
    static async Task Pick(IRenderedComponent<RequiredFieldsHost> cut, IRenderedComponent<NsSelect<SelectRef, Guid>> select)
    {
        await select.Find("div.mud-input-control").MouseDownAsync(new MouseEventArgs());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-list-item")), TimeSpan.FromSeconds(5));

        await cut.FindAll(".mud-list-item")[0].ClickAsync(new MouseEventArgs());
    }

    // A browsed lookup opens on its whole set the moment the box takes focus
    // (NsAutocompleteSearchTests), and the pick is the same click on the same node.
    static async Task Pick(IRenderedComponent<RequiredFieldsHost> cut, IRenderedComponent<NsAutocomplete<SelectRef, Guid>> lookup)
    {
        await lookup.Find("input").FocusAsync(new FocusEventArgs());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-list-item")), TimeSpan.FromSeconds(5));

        await cut.FindAll(".mud-list-item")[0].ClickAsync(new MouseEventArgs());
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

    /// <summary>The report's Tipo: a required Guid behind a select, refused empty, answered.</summary>
    [Fact]
    public async Task APickLiftsTheRefusalOnTheSelect_WithNoSecondSubmit()
    {
        var model = new RequiredFieldsModel();
        var cut = Mount(model);

        await Save(cut);

        Refuses(Select(cut));

        await Pick(cut, Select(cut));

        Assert.Equal(RequiredFieldsHost.Option.Id, model.PatientId);
        Accepts(Select(cut));
    }

    /// <summary>The report's Condición fiscal: the same sentinel behind the other control, which
    /// is why the answer lives in the base both of them inherit.</summary>
    [Fact]
    public async Task APickLiftsTheRefusalOnTheAutocomplete_WithNoSecondSubmit()
    {
        var model = new RequiredFieldsModel();
        var cut = Mount(model);

        await Save(cut);

        Refuses(Lookup(cut));

        await Pick(cut, Lookup(cut));

        Assert.Equal(RequiredFieldsHost.Option.Id, model.CategoryId);
        Accepts(Lookup(cut));
    }

    /// <summary>The lift does not weaken the gate: the field that was answered goes quiet and
    /// the ones still empty keep refusing the save. Name is answered so the only thing holding
    /// the submit is the field's own check — the annotation validator would refuse a null Name
    /// on its own and the gate would read green even if this half had gone soft.</summary>
    [Fact]
    public async Task AFormStillMissingAValue_IsStillRefused()
    {
        var model = new RequiredFieldsModel { Name = "Ok" };
        var submitted = false;
        var cut = Mount(model, () => submitted = true);

        await Save(cut);
        await Pick(cut, Select(cut));

        Accepts(Select(cut));
        Refuses(Lookup(cut));

        await Save(cut);

        Assert.False(submitted);
        Refuses(Lookup(cut));
    }

    /// <summary>And nothing is drawn before the form asks. OwnProblem() answers for any empty
    /// required field, a form nobody submitted included, so the lift is gated on the request
    /// having happened — otherwise a create form would open already refused.</summary>
    [Fact]
    public void AFreshFormDrawsNoRefusal()
    {
        var cut = Mount(new RequiredFieldsModel());

        Assert.DoesNotContain("Problems.Required", cut.Markup);
        Assert.Empty(cut.FindAll(".mud-input-error"));
    }

    /// <summary>The same silence survives a pick: a value arriving on a form nobody submitted
    /// re-asks the field's own store, and that store must stay empty rather than take the
    /// chance to refuse the fields beside it.</summary>
    [Fact]
    public async Task APickBeforeTheFirstSubmitDrawsNoRefusal()
    {
        var cut = Mount(new RequiredFieldsModel());

        await Pick(cut, Select(cut));

        Assert.DoesNotContain("Problems.Required", cut.Markup);
        Assert.Empty(cut.FindAll(".mud-input-error"));
    }

    /// <summary>And the tracking runs both ways once the form has asked: emptying a field that
    /// was answered puts its refusal back without a second Save. Only the autocomplete is empty
    /// at the submit, so the select starts accepted and the clear is the whole gesture.</summary>
    [Fact]
    public async Task ClearingAnAnsweredFieldAfterTheFirstSubmit_RefusesAgain()
    {
        var model = new RequiredFieldsModel { Name = "Ok", PatientId = RequiredFieldsHost.Option.Id };
        var cut = Mount(model, clearable: true);

        await Save(cut);

        Accepts(Select(cut));

        await Select(cut).Find("button.mud-input-clear-button").ClickAsync(new MouseEventArgs());

        Assert.Equal(Guid.Empty, model.PatientId);
        Refuses(Select(cut));
    }
}
