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

/// <summary>What a box visibly holds is what the form submits. The fields bound on the
/// vendor's change event instead, which is blur: a Nombre typed and Guardar pressed without
/// leaving the field was refused as Obligatorio under a field showing the text, and a second
/// press — the blur having landed by then — saved (nsail#1937). An `input` event with no
/// `change` after it is exactly that gesture, so each of these types once and submits without
/// leaving the box.</summary>
public sealed class TypedValueSubmitTests : BunitContext, IAsyncLifetime
{
    public TypedValueSubmitTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
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

    /// <summary>The reported story: the required name is the only answer the form needs, and
    /// typing it is the only act before the submit.</summary>
    [Fact]
    public async Task TypingTheRequiredNameAndSubmitting_SavesOnTheFirstPress()
    {
        var model = new TypedValueModel();
        TypedValueModel? sent = null;

        var cut = Render<TypedValueHost>(p => p
            .Add(x => x.Model, model)
            .Add(x => x.Submitted, submitted => sent = submitted));

        await Type<NsTextField>(cut, "Feriado");
        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.NotNull(sent);
        Assert.Equal("Feriado", sent.Name);
        Assert.Empty(cut.FindComponent<NsTextField>().FindAll(".mud-input-error"));
    }

    /// <summary>Every box a value is typed into, one at a time: the family is what the fix
    /// lands in, so a field added later that binds on change is named here by the member that
    /// arrived empty rather than found by a walker on one screen.</summary>
    [Fact]
    public async Task TypingIntoAnyTextShapedField_ReachesTheModelWithoutLeavingIt()
    {
        var model = new TypedValueModel { Name = "Feriado" };

        var cut = Render<TypedValueHost>(p => p
            .Add(x => x.Model, model)
            .Add(x => x.Submitted, _ => { }));

        await Type<NsTextArea>(cut, "Una nota");
        await Type<NsEmailField>(cut, "alguien@acme.example");
        await Type<NsPhoneField>(cut, "1122334455");
        await Type<NsUrlField>(cut, "https://acme.example");
        await Type<NsPasswordField>(cut, "secreto");
        await Type<NsSearchField>(cut, "buscado");
        await Type<NsNumericField<decimal?>>(cut, "7");
        await Type<NsMoneyField<decimal?>>(cut, "1500");
        await Type<NsPercentField<decimal?>>(cut, "21");
        await Type<NsDurationField<TimeSpan?>>(cut, "3");

        Assert.Equal("Una nota", model.Note);
        Assert.Equal("alguien@acme.example", model.MailAddress);
        Assert.Equal("1122334455", model.Phone);
        Assert.Equal("https://acme.example", model.Site);
        Assert.Equal("secreto", model.Secret);
        Assert.Equal("buscado", model.Search);
        Assert.Equal(7m, model.Amount);
        Assert.Equal(1500m, model.Price);
        Assert.Equal(21m, model.Share);
        Assert.Equal(TimeSpan.FromHours(3), model.Length);
    }

    /// <summary>The cost the fix must not pay: a figure box now hears every keystroke, and a
    /// decimal is typed through a state ("1," / "1.") that reads as a whole number. The box
    /// must keep drawing what was typed rather than reformatting over the separator, in both
    /// notations the house reads (testing.md: the same literal is two numbers).</summary>
    [Theory]
    [InlineData("en", "1.25")]
    [InlineData("es", "1,25")]
    public async Task TypingAFigureKeystrokeByKeystroke_NeverRewritesTheHalfTypedSeparator(string language, string typed)
    {
        Services.AddSingleton(new LanguageProvider { Current = language });

        var model = new TypedValueModel { Name = "Feriado" };

        var cut = Render<TypedValueHost>(p => p
            .Add(x => x.Model, model)
            .Add(x => x.Submitted, _ => { }));

        for (var length = 1; length <= typed.Length; length++)
        {
            var prefix = typed[..length];

            await Type<NsNumericField<decimal?>>(cut, prefix);

            Assert.Equal(prefix, Box<NsNumericField<decimal?>>(cut).GetAttribute("value"));
        }

        Assert.Equal(1.25m, model.Amount);
    }

    // The gesture under test is the keystroke alone: Input raises `input` and nothing else, so
    // nothing here ever blurs the box. A field that still bound on change throws
    // MissingEventHandlerException from this line — the input has no oninput handler at all.
    static Task Type<TField>(IRenderedComponent<TypedValueHost> cut, string text) where TField : IComponent
    {
        return cut.InvokeAsync(() => Box<TField>(cut).Input(text));
    }

    static AngleSharp.Dom.IElement Box<TField>(IRenderedComponent<TypedValueHost> cut) where TField : IComponent
    {
        return cut.FindComponent<TField>().Find("input, textarea");
    }
}
