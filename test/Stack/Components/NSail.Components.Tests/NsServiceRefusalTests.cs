// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;
using NSail.Messaging.Runtime.Validation;

namespace NSail.Components.Tests;

/// <summary>An attribute that reads a service through ValidationContext.GetService answers on
/// the screen from the component's own scope — on submit, on a field change after it, and when
/// a table's or a list editor's open row is settled — and is worded from its code's catalog row,
/// never from its own ErrorMessage.</summary>
public sealed class NsServiceRefusalTests : BunitContext, IAsyncLifetime
{
    const string Words = "Cantidad de dígitos incorrecta";

    public NsServiceRefusalTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            ["Problems.DigitsByRule"] = Words,
        })]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    IRenderedComponent<ServiceRefusalHost> RenderHost(ServiceRefusalModel model, bool rule = true)
    {
        if (rule)
        {
            Services.AddScoped<DigitRule>();
        }

        return Render<ServiceRefusalHost>(p => p.Add(x => x.Model, model));
    }

    static MudTextField<string> Field(IRenderedComponent<ServiceRefusalHost> cut, string label)
    {
        return cut.FindComponents<MudTextField<string>>().Single(field => field.Instance.Label == label).Instance;
    }

    static string? Refusal(IRenderedComponent<ServiceRefusalHost> cut, string label)
    {
        var field = Field(cut, label);

        return field.Error ? field.ErrorText : null;
    }

    static Task Submit(IRenderedComponent<ServiceRefusalHost> cut)
    {
        return cut.InvokeAsync(() => cut.Find("form").Submit());
    }

    [Fact]
    public async Task ASubmitIsRefusedByTheServiceTheAttributeReads_InItsCodesWords()
    {
        var cut = RenderHost(new ServiceRefusalModel { Number = "123" });

        await Submit(cut);

        Assert.Equal(0, cut.Instance.Submits);
        Assert.Equal(Words, Refusal(cut, "Number"));
    }

    [Fact]
    public async Task AFieldChangeAfterTheSubmitAsksTheServiceAgain()
    {
        var cut = RenderHost(new ServiceRefusalModel { Number = "123" });

        await Submit(cut);

        await cut.InvokeAsync(() => cut.Find("input").Input("1234567"));

        Assert.Null(Refusal(cut, "Number"));

        await cut.InvokeAsync(() => cut.Find("input").Input("12"));

        Assert.Equal(Words, Refusal(cut, "Number"));
    }

    [Fact]
    public async Task AValueTheServiceTakesGoesThrough()
    {
        var cut = RenderHost(new ServiceRefusalModel { Number = "1234567" });

        await Submit(cut);

        Assert.Equal(1, cut.Instance.Submits);
    }

    [Fact]
    public async Task WithNoServiceRegisteredTheAttributeDecides()
    {
        var cut = RenderHost(new ServiceRefusalModel { Number = "123" }, rule: false);

        await Submit(cut);

        Assert.Equal(1, cut.Instance.Submits);
        Assert.Null(Refusal(cut, "Number"));
    }

    [Fact]
    public async Task AnOpenTableRowIsRefusedOnSettleByTheServiceItsAttributeReads()
    {
        var model = new ServiceRefusalModel { Number = "1234567", Lines = [new ServiceRefusalRow { Number = "1234567" }] };
        var cut = RenderHost(model);

        await cut.InvokeAsync(() => cut.FindAll("button[aria-label='Actions.Edit']")[0].Click());
        await cut.InvokeAsync(() => cut.FindAll("input")[1].Input("123"));
        await Submit(cut);

        Assert.Equal(0, cut.Instance.Submits);
        Assert.Equal(Words, Refusal(cut, "Line"));
    }

    [Fact]
    public async Task AnOpenListRowIsRefusedOnSettleByTheServiceItsAttributeReads()
    {
        var model = new ServiceRefusalModel { Number = "1234567", Items = [new ServiceRefusalRow { Number = "1234567" }] };
        var cut = RenderHost(model);

        await cut.InvokeAsync(() => cut.FindAll("button[aria-label='Actions.Edit']")[0].Click());
        await cut.InvokeAsync(() => cut.FindAll("input")[1].Input("123"));
        await Submit(cut);

        Assert.Equal(0, cut.Instance.Submits);
        Assert.Equal(Words, Refusal(cut, "Item"));
    }

    /// <summary>The words a product's own editor draws through the public ladder: with the
    /// provider the result was validated with, the code's row; without one, as before, the
    /// attribute — which then refuses nothing — is not found failing and the result's own text
    /// stands.</summary>
    [Fact]
    public void RefusalWordsFindsTheFailingAttributeWithTheProviderItIsHanded()
    {
        Services.AddScoped<DigitRule>();

        var strings = Services.GetRequiredService<StringManager>();
        var model = new ServiceRefusalRow { Number = "123" };
        var result = new System.ComponentModel.DataAnnotations.ValidationResult("The field Number has the wrong digits.");

        Assert.Equal(Words, RefusalWords.For(strings, model, nameof(ServiceRefusalRow.Number), result, Services));
        Assert.Equal(result.ErrorMessage, RefusalWords.For(strings, model, nameof(ServiceRefusalRow.Number), result));
    }

    [Fact]
    public void TheWireRefusesTheSameRowWithTheSameCode()
    {
        Services.AddScoped<DigitRule>();

        var problem = MessageValidator.Validate(new ServiceRefusalModel { Items = [new ServiceRefusalRow { Number = "123" }] }, Services);

        var issue = Assert.Single(problem!.Issues!);

        Assert.Equal("DigitsByRule", issue.Code);
        Assert.Equal("Items[0].Number", issue.Source);
    }
}
