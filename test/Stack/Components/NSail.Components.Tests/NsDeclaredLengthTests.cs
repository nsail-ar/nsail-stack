// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>A box used to let anyone type what its own message would refuse: every string
/// member on the wire declares a length, and no field read it, so the input rendered the
/// vendor's 524288 and a 38-character paste into a 30-character number went out to be refused
/// by the handler. The cap is now derived from the declaration that refuses — the same reading
/// behind the required mark and the calendar's ceiling, under the same scope — and no page
/// names a figure.</summary>
public sealed class NsDeclaredLengthTests : BunitContext, IAsyncLifetime
{
    public NsDeclaredLengthTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>())]));
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

    IRenderedComponent<DeclaredLengthHost> RenderHost(DeclaredLengthModel? model = null)
    {
        return Render<DeclaredLengthHost>(p => p.Add(x => x.Model, model ?? new DeclaredLengthModel()));
    }

    static IElement Box(IRenderedComponent<DeclaredLengthHost> cut, string label)
    {
        return (IElement)cut.FindAll(".mud-input-control")
            .Single(node => node.QuerySelector("label")?.TextContent == label)
            .QuerySelector("input, textarea")!;
    }

    static string? Cap(IRenderedComponent<DeclaredLengthHost> cut, string label)
    {
        return Box(cut, label).GetAttribute("maxlength");
    }

    /// <summary>The reported case: the number box of a message whose member declares
    /// [MaxLength(30)] refuses the 31st character, so the paste that used to be sent and
    /// refused cannot be entered.</summary>
    [Fact]
    public void ABoxTakesItsCapFromTheDeclaredMaxLength()
    {
        Assert.Equal("30", Cap(RenderHost(), "Number"));
    }

    /// <summary>The other annotation that refuses a length on the wire — the validator folds
    /// both into one MaxLength refusal, so the box reads both as one bound.</summary>
    [Fact]
    public void ABoxTakesItsCapFromADeclaredStringLength()
    {
        Assert.Equal("12", Cap(RenderHost(), "Code"));
    }

    /// <summary>A subclass of either comes back from the same reading with nothing added:
    /// [SearchTerm] is a MaxLength whose figure lives in one place.</summary>
    [Fact]
    public void ABoxTakesItsCapFromASubclassOfTheAnnotation()
    {
        Assert.Equal("100", Cap(RenderHost(), "Term"));
    }

    /// <summary>A member declaring no length keeps the vendor's own ceiling: a field draws a
    /// bound it has and never invents one.</summary>
    [Fact]
    public void ABoxWhoseMemberDeclaresNothingKeepsTheVendorsCeiling()
    {
        Assert.Equal("524288", Cap(RenderHost(), "Free"));
    }

    /// <summary>The scope is the required mark's, which is the wire's: a model a member opened
    /// with [Validated] is refused at both ends and capped like the form's own.</summary>
    [Fact]
    public void AModelOpenedToTheFormIsCappedLikeTheFormsOwn()
    {
        Assert.Equal("8", Cap(RenderHost(), "Opened"));
    }

    /// <summary>And a row model nothing opened is refused by neither end, so it is capped by
    /// neither — the same declaration, read under a scope that does not reach it.</summary>
    [Fact]
    public void AModelNothingOpenedIsCappedByNothing()
    {
        Assert.Equal("524288", Cap(RenderHost(), "Loose"));
    }

    /// <summary>The figure family is out: what refuses a number is its range, and a character
    /// count over a formatted figure would measure the group separators. The member here
    /// declares a length on purpose — the box has something to ignore.</summary>
    [Fact]
    public void AFigureBoxDrawsNoCapThoughItsMemberDeclaresALength()
    {
        Assert.Equal("524288", Cap(RenderHost(), "Price"));
    }

    /// <summary>A masked box is bounded by its pattern and by nothing else: the vendor's mask
    /// input carries no `maxlength` at all, which is what keeps the derivation safe there —
    /// the declaration bounds the RAW value while the box holds the FORMATTED text, so a cap
    /// landing on it would stop the typing short of the value's own last character (a bound of
    /// 11 under "00-00000000-0" at the eleventh digit). Measured, because the day the vendor
    /// starts drawing one this test is the warning.</summary>
    [Fact]
    public void AMaskedBoxIsBoundedByItsPatternAndCarriesNoCap()
    {
        Assert.Null(Cap(RenderHost(), "Masked"));
    }

    /// <summary>A value already longer than the cap — a column that used to be wider, a record
    /// from an import — opens showing all of it and keeps it: the cap refuses the next
    /// character and never truncates what the model holds, which is the behaviour that makes
    /// deriving it safe on a screen nobody edits.</summary>
    [Fact]
    public void AValueLongerThanTheCapStillDisplaysWhole_AndTheModelKeepsIt()
    {
        var pasted = "Telefono: +54 9 341 597-4793 (celular)";
        var model = new DeclaredLengthModel { Number = pasted };

        var cut = RenderHost(model);

        Assert.Equal(pasted, Box(cut, "Number").GetAttribute("value"));
        Assert.Equal(pasted, model.Number);
    }
}
