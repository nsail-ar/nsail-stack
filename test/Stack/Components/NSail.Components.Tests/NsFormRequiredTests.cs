// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>Leonardo, 2026-08-03: saving the OT without a paciente showed the API's 404
/// instead of the required marker under the field — EditContext.Validate() had no subscriber
/// to OnValidationRequested, so it always answered "valid" no matter what the model declared.
/// NsForm now carries its own DataAnnotationsValidator (fixes Name, a plain [Required]
/// string), and NsFieldBase enforces its own Required parameter directly (fixes PatientId's
/// family: a Guid PolicyField whose sentinel is Guid.Empty, which RequiredAttribute cannot
/// see — confirmed against the BCL, not assumed). Both must block the same submit and speak
/// under their own field before anything is sent.</summary>
public sealed class NsFormRequiredTests : BunitContext, IAsyncLifetime
{
    public NsFormRequiredTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // Same MudPopoverProvider teardown rule as NsSelectTests: bunit's synchronous Dispose
    // cannot tear down MudBlazor's popover provider, so xunit's async lifecycle takes over.
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    // Each of these answers every required field but the one under test, and reads the refusal
    // off that field's own component rather than off the whole form: the host seats three
    // required fields, so a form-wide read is satisfied by any of them and the test stops
    // naming the one it is about.
    [Fact]
    public async Task SavingWithoutName_BlocksSubmitAndSpeaksUnderTheStringField()
    {
        var model = new RequiredFieldsModel { Name = null, PatientId = Guid.NewGuid(), CategoryId = Guid.NewGuid() };
        var submitted = false;

        var cut = Render<RequiredFieldsHost>(p => p
            .Add(x => x.Model, model)
            .Add(x => x.Submitted, () => submitted = true));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.False(submitted);
        Assert.NotEmpty(cut.FindComponent<NsTextField>().FindAll(".mud-input-error"));
    }

    /// <summary>The native gate, lifted house-wide: a marked field hands the input the HTML
    /// `required` attribute, and in a real form without `novalidate` Chromium's own constraint
    /// validation stopped the press before Blazor saw it — its bubble, its English, outside all
    /// three of the house's refusal placements. The form withdraws the browser's UI and keeps
    /// the mark, which is what an assistive technology reads.</summary>
    [Fact]
    public void TheFormWithdrawsTheBrowsersOwnValidation_AndKeepsTheInputsRequiredMark()
    {
        var model = new RequiredFieldsModel { Name = null, PatientId = Guid.NewGuid(), CategoryId = Guid.NewGuid() };

        var cut = Render<RequiredFieldsHost>(p => p
            .Add(x => x.Model, model)
            .Add(x => x.Submitted, () => { }));

        Assert.True(cut.Find("form").HasAttribute("novalidate"));
        Assert.True(cut.FindComponent<NsTextField>().Find("input").HasAttribute("required"));
    }

    /// <summary>The row this fixes: Name carries a plain DataAnnotations [Required], and the
    /// vendor's DataAnnotationsValidator rendered its own English-only default text
    /// ("The Name field is required.") no matter the session's language. NsDataAnnotationsValidator
    /// speaks through Problems.Required instead — the same source every other Problem uses — so
    /// an es session reads "Obligatorio" and an en session reads "Required", never the BCL
    /// default in either.</summary>
    [Theory]
    [InlineData("en", "Required")]
    [InlineData("es", "Obligatorio")]
    public async Task SavingWithoutName_SpeaksTheAppsOwnRequiredWord_NotTheBclDefault(string language, string expected)
    {
        Services.AddSingleton(new StringCatalog([new TwoLanguageStrings("Problems.Required", "Required", "Obligatorio")]));
        Services.AddSingleton(new LanguageProvider { Current = language });

        var model = new RequiredFieldsModel { Name = null, PatientId = Guid.NewGuid(), CategoryId = Guid.NewGuid() };

        var cut = Render<RequiredFieldsHost>(p => p
            .Add(x => x.Model, model)
            .Add(x => x.Submitted, () => { }));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.Contains(expected, cut.FindComponent<NsTextField>().Markup);
        Assert.DoesNotContain("field is required", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The OT scenario itself: PatientId carries no DataAnnotations attribute at all
    /// (required Guid, RequiredAttribute is a no-op against Guid.Empty) — only NsFieldBase's
    /// own Required parameter, now wired to EditContext, can catch it.</summary>
    [Fact]
    public async Task SavingWithoutPatientId_BlocksSubmitAndSpeaksUnderTheGuidField()
    {
        var model = new RequiredFieldsModel { Name = "Ok", PatientId = Guid.Empty, CategoryId = Guid.NewGuid() };
        var submitted = false;

        var cut = Render<RequiredFieldsHost>(p => p
            .Add(x => x.Model, model)
            .Add(x => x.Submitted, () => submitted = true));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.False(submitted);
        Assert.Contains("Problems.Required", cut.FindComponent<NsSelect<SelectRef, Guid>>().Markup);
    }

    [Fact]
    public async Task FixingEveryField_Submits()
    {
        var model = new RequiredFieldsModel { Name = "Ok", PatientId = Guid.NewGuid(), CategoryId = Guid.NewGuid() };
        var submitted = false;

        var cut = Render<RequiredFieldsHost>(p => p
            .Add(x => x.Model, model)
            .Add(x => x.Submitted, () => submitted = true));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.True(submitted);
    }
}
