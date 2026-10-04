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

/// <summary>The mark is DERIVED, never written (nsail#797): a field whose bound member
/// declares [Required] draws as required with no page saying so, which is what stops one
/// screen from marking some of its required fields and not others. The explicit parameter
/// survives as the override for what an annotation cannot express — a conditional
/// requirement, and a non-nullable binding whose "empty" is a sentinel RequiredAttribute
/// never rejects.</summary>
public sealed class NsFieldRequiredMarkTests : BunitContext, IAsyncLifetime
{
    public NsFieldRequiredMarkTests()
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

    [Fact]
    public void AnAnnotatedMemberIsMarkedWithNoPageSayingSo()
    {
        var cut = Render();

        Assert.True(IsMarked(cut, "Declared"));
    }

    [Fact]
    public void AMemberThatDeclaresNothingIsNotMarked()
    {
        var cut = Render();

        Assert.False(IsMarked(cut, "Plain"));
    }

    /// <summary>The sentinel family: a Guid bound as Guid has no DataAnnotations path at all,
    /// so the parameter is the only thing that can mark it — and it still does.</summary>
    [Fact]
    public void TheExplicitParameterStillMarksAMemberThatDeclaresNothing()
    {
        var cut = Render();

        Assert.True(IsMarked(cut, "Sentinel"));
    }

    /// <summary>RequiredAttribute passes ANY non-null boxed value, so on a non-nullable value
    /// type it is already satisfied at the type's own default and refuses nothing. A mark
    /// derived from it would be exactly the lie this derivation exists to end — an asterisk on
    /// a field the form then accepts empty.</summary>
    [Fact]
    public async Task AnAnnotationThatCanNeverRefuseIsNeitherMarkedNorRefused()
    {
        var model = new RequiredDerivationModel { Declared = "Ok", Sentinel = Guid.NewGuid(), Enforced = 1 };
        var submitted = false;

        var cut = Render(model, () => submitted = true);

        Assert.False(IsMarked(cut, "Counted"));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.True(submitted);
        Assert.Equal(0, model.Counted);
    }

    /// <summary>What is left for the types the annotation cannot see: the explicit parameter,
    /// which mints the mark and the field's own sentinel refusal off one flag, so on those
    /// types the two cannot disagree either.</summary>
    [Fact]
    public async Task TheExplicitParameterMarksAndRefusesANonNullableValueType()
    {
        var model = new RequiredDerivationModel { Declared = "Ok", Sentinel = Guid.NewGuid() };
        var submitted = false;

        var cut = Render(model, () => submitted = true);

        Assert.True(IsMarked(cut, "Enforced"));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.False(submitted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AConditionalRequirementStaysConditional(bool condition)
    {
        var cut = Render(new RequiredDerivationModel { Conditional = condition });

        Assert.Equal(condition, IsMarked(cut, "Conditioned"));
    }

    /// <summary>The scope rule: NsDataAnnotationsValidator validates the EditContext's model
    /// and nothing nested under it — the same scope MessageValidator walks on the wire — so a
    /// [Required] on a row model refuses nothing and must not be drawn as though it did.</summary>
    [Fact]
    public async Task ARowModelsAnnotationIsNeitherMarkedNorRefused()
    {
        var model = new RequiredDerivationModel { Declared = "Ok", Sentinel = Guid.NewGuid(), Enforced = 1 };
        var submitted = false;

        var cut = Render(model, () => submitted = true);

        Assert.False(IsMarked(cut, "Line"));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.True(submitted);
    }

    /// <summary>The mark and the refusal answer for the same member: what is drawn required
    /// is what stops the submit, with nothing on the page repeating either.</summary>
    [Fact]
    public async Task TheDerivedMarkRefusesTheSubmitItPromised()
    {
        var model = new RequiredDerivationModel { Declared = null, Sentinel = Guid.NewGuid() };
        var submitted = false;

        var cut = Render(model, () => submitted = true);

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.False(submitted);
        Assert.NotEmpty(cut.FindAll(".mud-input-error"));
    }

    IRenderedComponent<RequiredDerivationHost> Render(RequiredDerivationModel? model = null, Action? submitted = null)
    {
        return Render<RequiredDerivationHost>(p => p
            .Add(x => x.Model, model ?? new RequiredDerivationModel())
            .Add(x => x.Submitted, () => submitted?.Invoke()));
    }

    // The asterisk itself is drawn by the vendor's stylesheet off this class, so the class is
    // what a rendered test can read.
    static bool IsMarked(IRenderedComponent<RequiredDerivationHost> cut, string label)
    {
        var control = cut.FindAll(".mud-input-control")
            .Single(node => node.QuerySelector("label")?.TextContent == label);

        return control.ClassList.Contains("mud-input-required");
    }
}
