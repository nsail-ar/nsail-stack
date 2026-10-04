// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Dates;
using NSail.Localization;
using NSail.Messaging.Runtime.Validation;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>Half of a refusal used to be untranslatable: the validator wrote house words for
/// [Required] and [Compare] and passed every other attribute's own ErrorMessage through, so a
/// rule outside the BCL's vocabulary reached a Spanish screen in whatever language it was
/// written in. An attribute that names its own problem code (ICodedValidation) is now worded
/// from the catalog by the SAME ladder MessageValidator's Issue takes on the wire —
/// "Problems.{Code}.{field}", then "Problems.{Code}" — so the screen and the server refusal are
/// one sentence in one file. The attribute under test is declared in this assembly on purpose:
/// the seam carries a rule the Stack does not own, and the Stack supplies no domain for it.</summary>
public sealed class NsCodedRefusalTests : BunitContext, IAsyncLifetime
{
    public NsCodedRefusalTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            ["Problems.NotFuture"] = "No puede ser posterior a hoy",
            ["Problems.NotNegative"] = "No puede ser menor que cero",
            ["Problems.NotAbove"] = "No puede ser mayor que el límite superior",
            ["Problems.NotAbove.From"] = "Desde no puede ser mayor que Hasta",
            ["Problems.Sample"] = "No es una muestra",
            ["Problems.Sample.Scoped"] = "Este campo no es una muestra"
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

    IRenderedComponent<CodedRefusalHost> RenderHost(CodedRefusalModel model, Action? submitted = null)
    {
        return Render<CodedRefusalHost>(p => p
            .Add(x => x.Model, model)
            .Add(x => x.Submitted, () => submitted?.Invoke()));
    }

    static string? Refusal(IRenderedComponent<CodedRefusalHost> cut, string label)
    {
        return cut.FindAll(".mud-input-control")
            .Single(node => node.QuerySelector("label")?.TextContent == label)
            .QuerySelector(".mud-input-helper-text")
            ?.TextContent;
    }

    static MudDatePicker Picker(IRenderedComponent<CodedRefusalHost> cut, string label)
    {
        return cut.FindComponents<MudDatePicker>()
            .Single(component => component.Instance.Label == label)
            .Instance;
    }

    /// <summary>The calendar's own ceiling is derived from the bound member's [NotFuture], with
    /// no page naming a date: the field that will refuse a later day does not offer one
    /// first.</summary>
    [Fact]
    public void ABoundedDateFieldTakesItsCeilingFromTheDeclaration()
    {
        var cut = RenderHost(new CodedRefusalModel());

        Assert.Equal(BusinessDate.Today.ToDateTime(TimeOnly.MinValue), Picker(cut, "Birth").MaxDate);
    }

    [Fact]
    public void ADateFieldWhoseMemberDeclaresNothingKeepsAnUnboundedCalendar()
    {
        var cut = RenderHost(new CodedRefusalModel());

        Assert.Null(Picker(cut, "Any").MaxDate);
    }

    /// <summary>A row already on file with a date past the ceiling still opens showing it: the
    /// vendor reads MaxDate when a day is picked or typed, not when one is handed in, so
    /// deriving the bound cannot quietly empty a field nobody touched. Measured, because the
    /// opposite behaviour would destroy exactly the data this story went looking for.</summary>
    [Fact]
    public void AValueAlreadyPastTheCeilingStillDisplays_AndTheModelKeepsIt()
    {
        var future = BusinessDate.Today.AddYears(4);
        var model = new CodedRefusalModel { Birth = future };

        var cut = RenderHost(model);

        Assert.Equal(future, model.Birth);
        Assert.Equal(future.ToDateTime(TimeOnly.MinValue), Picker(cut, "Birth").Date);
    }

    /// <summary>The refusal itself, in the catalog's words: the generic rung of the ladder,
    /// which is where a rule with one sentence for every field lives.</summary>
    [Fact]
    public async Task AFutureDateIsRefusedOnTheField_InTheCatalogsWords()
    {
        var submitted = false;
        var cut = RenderHost(new CodedRefusalModel { Birth = BusinessDate.Today.AddDays(1) }, () => submitted = true);

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.False(submitted);
        Assert.Equal("No puede ser posterior a hoy", Refusal(cut, "Birth"));
    }

    [Fact]
    public async Task TodayIsNotInTheFuture_AndTheSubmitGoesThrough()
    {
        var submitted = false;
        var cut = RenderHost(new CodedRefusalModel { Birth = BusinessDate.Today }, () => submitted = true);

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.True(submitted);
        Assert.Null(Refusal(cut, "Birth"));
    }

    [Fact]
    public async Task ACodedRefusalReadsTheCatalogRowForItsCode()
    {
        var cut = RenderHost(new CodedRefusalModel { Coded = "nope" });

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.Equal("No es una muestra", Refusal(cut, "Coded"));
    }

    /// <summary>The specific rung wins where the catalog has one, exactly as StringManager
    /// resolves an Issue the server sent: one rule, two fields, two sentences.</summary>
    [Fact]
    public async Task ARowForTheFieldItselfWinsOverTheGenericOne()
    {
        var cut = RenderHost(new CodedRefusalModel { Scoped = "nope" });

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.Equal("Este campo no es una muestra", Refusal(cut, "Scoped"));
    }

    /// <summary>Non-vacuity: an attribute that names no code is untouched, so this is not
    /// DataAnnotations localization at large arriving by the back door.</summary>
    [Fact]
    public async Task AnUncodedAttributeKeepsItsOwnErrorMessage()
    {
        var cut = RenderHost(new CodedRefusalModel { Sized = "abcd" });

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.Equal("The field Sized must be a string or array type with a maximum length of '3'.", Refusal(cut, "Sized"));
    }

    /// <summary>The wire says the same thing about the same model: one attribute, one code, and
    /// the field it names is the field the screen drew under.</summary>
    [Fact]
    public void TheWireRefusesTheSameModelWithTheSameCode()
    {
        var problem = MessageValidator.Validate(new CodedRefusalModel { Birth = BusinessDate.Today.AddDays(1) });

        Assert.NotNull(problem);

        var issue = Assert.Single(problem.Issues!);

        Assert.Equal("NotFuture", issue.Code);
        Assert.Equal("Birth", issue.Source);
    }

    /// <summary>nsail#820's first half on the screen: a slipped minus sign is refused under the
    /// field that took it, in the catalog's words rather than the attribute's English.</summary>
    [Fact]
    public async Task ANegativeNumberIsRefusedOnTheField_InTheCatalogsWords()
    {
        var submitted = false;
        var cut = RenderHost(new CodedRefusalModel { Price = -100m }, () => submitted = true);

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.False(submitted);
        Assert.Equal("No puede ser menor que cero", Refusal(cut, "Price"));
    }

    /// <summary>Zero is not negative: an unpriced article is a real state, and a floor that
    /// refused its own edge would be a different floor.</summary>
    [Fact]
    public async Task ZeroIsTaken_AndTheSubmitGoesThrough()
    {
        var submitted = false;
        var cut = RenderHost(new CodedRefusalModel { Price = 0m }, () => submitted = true);

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.True(submitted);
        Assert.Null(Refusal(cut, "Price"));
    }

    /// <summary>The cross-field half: the refusal is drawn under the member that went past the
    /// other, and the row for that field wins over the generic one.</summary>
    [Fact]
    public async Task ADesdeAboveItsHastaIsRefusedUnderTheDesde()
    {
        var submitted = false;
        var cut = RenderHost(new CodedRefusalModel { From = 4m, To = 2m }, () => submitted = true);

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.False(submitted);
        Assert.Equal("Desde no puede ser mayor que Hasta", Refusal(cut, "From"));
        Assert.Null(Refusal(cut, "To"));
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(-2, 2)]
    public async Task APairInOrderIsTaken(double from, double to)
    {
        var submitted = false;
        var cut = RenderHost(new CodedRefusalModel { From = (decimal)from, To = (decimal)to }, () => submitted = true);

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.True(submitted);
        Assert.Null(Refusal(cut, "From"));
    }

    /// <summary>Both bounds, on the wire, off the same declaration the screen just refused by —
    /// the whole point of declaring them on the message instead of in a handler.</summary>
    [Fact]
    public void TheWireRefusesBothBoundsOnTheSameModel()
    {
        var problem = MessageValidator.Validate(new CodedRefusalModel { Price = -100m, From = 4m, To = 2m });

        Assert.NotNull(problem);

        Assert.Equal(
            [("NotNegative", "Price"), ("NotAbove", "From")],
            problem.Issues!.Select(issue => (issue.Code, issue.Source)));
    }
}
