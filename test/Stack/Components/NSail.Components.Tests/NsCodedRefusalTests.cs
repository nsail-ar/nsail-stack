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

/// <summary>A refusal used to be untranslatable wherever the vocabulary ran out: the validator
/// wrote house words for [Required], [Compare] and any ICodedValidation, and passed every other
/// attribute's own ErrorMessage through — so a [MaxLength] or a [RegularExpression] reached a
/// Spanish screen in the BCL's English, naming the member and quoting the pattern. Every
/// attribute is now worded from the catalog by the SAME switch that mints the wire's Issue
/// (MessageValidator.IssueFor), resolved by the SAME ladder — "Problems.{Code}.{field}", then
/// "Problems.{Code}" — so the screen and the server refusal are one sentence in one file, and
/// a rule outside the vocabulary gets the house's generic word rather than its own English.
/// Coded is still what makes a field SAY its rule, which is what the last tests here hold.
/// The coded attribute under test is declared in this assembly on purpose: the seam carries a
/// rule the Stack does not own, and the Stack supplies no domain for it.</summary>
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
            ["Problems.Sample.Scoped"] = "Este campo no es una muestra",
            ["Problems.MaxLength"] = "No más de {max} caracteres",
            ["Problems.OutOfRange"] = "Entre {from} y {to}",
            ["Problems.InvalidFormat"] = "Formato inválido",
            ["Problems.InvalidFormat.Prefix"] = "Solo los dígitos, sin el 0",
            ["Problems.Invalid"] = "Valor inválido"
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

    /// <summary>The bound the attribute refuses by rides into the sentence as the catalog's own
    /// token, so the row is written once and says the number the declaration holds.</summary>
    [Fact]
    public async Task AMaxLengthIsRefusedInTheCatalogsWords_WithTheBoundFilledIn()
    {
        var cut = RenderHost(new CodedRefusalModel { Sized = "abcd" });

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.Equal("No más de 3 caracteres", Refusal(cut, "Sized"));
    }

    [Fact]
    public async Task ARangeIsRefusedInTheCatalogsWords_WithBothBoundsFilledIn()
    {
        var cut = RenderHost(new CodedRefusalModel { Count = 42 });

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.Equal("Entre 1 y 10", Refusal(cut, "Count"));
    }

    /// <summary>The leak this story was reported for: the pattern is the rule's machinery and
    /// the member is the model's name, and neither is anything to show a person.</summary>
    [Fact]
    public async Task APatternIsRefusedInTheCatalogsWords_WithNeitherTheRegexNorTheMemberShown()
    {
        var cut = RenderHost(new CodedRefusalModel { Digits = "abc" });

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        var refusal = Refusal(cut, "Digits");

        Assert.Equal("Formato inválido", refusal);
        Assert.DoesNotContain("^[0-9]+$", refusal, StringComparison.Ordinal);
        Assert.DoesNotContain("Digits", refusal, StringComparison.Ordinal);
    }

    /// <summary>Directory's Característica, in the Stack: a BCL code reaches the scoped rung
    /// too, so one field says its own rule with no attribute minted for it.</summary>
    [Fact]
    public async Task ARowForTheFieldWinsOverTheGenericOneForABclCodeToo()
    {
        var cut = RenderHost(new CodedRefusalModel { Prefix = "0221" });

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.Equal("Solo los dígitos, sin el 0", Refusal(cut, "Prefix"));
    }

    /// <summary>Non-vacuity, the other way around now: a rule the vocabulary has no code for
    /// gets the house's generic word, and what it may NOT reach the person as is the English
    /// it was written in.</summary>
    [Fact]
    public async Task AnAttributeOutsideTheVocabularyGetsTheHousesGenericWord()
    {
        var cut = RenderHost(new CodedRefusalModel { Odd = "nope" });

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.Equal("Valor inválido", Refusal(cut, "Odd"));
    }

    /// <summary>What the widened ladder does NOT take away: a coded attribute still says its
    /// own rule where the generic word is all the field would otherwise get.</summary>
    [Fact]
    public async Task ACodedAttributeStillSaysItsOwnRuleRatherThanTheGenericWord()
    {
        var cut = RenderHost(new CodedRefusalModel { Coded = "nope", Odd = "nope" });

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.Equal("No es una muestra", Refusal(cut, "Coded"));
        Assert.Equal("Valor inválido", Refusal(cut, "Odd"));
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

    /// <summary>One vocabulary, not two that agree: the code and the field the screen drew its
    /// sentence from are the ones the wire would send about the same value.</summary>
    [Fact]
    public void TheWireWordsAPatternWithTheCodeTheScreenJustDrewFrom()
    {
        var problem = MessageValidator.Validate(new CodedRefusalModel { Digits = "abc" });

        Assert.NotNull(problem);

        var issue = Assert.Single(problem.Issues!);

        Assert.Equal("InvalidFormat", issue.Code);
        Assert.Equal("Digits", issue.Source);
    }
}
