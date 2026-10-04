// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#198: the form owns the read of the model it edits. A read that failed leaves
/// no fields and no Save on screen — the blank form over an empty model was one click away from
/// writing nothing over stored settings — and a document that does not exist yet is a different
/// answer entirely: there the blank IS the data.</summary>
public sealed class NsFormLoadTests : BunitContext, IAsyncLifetime
{
    public NsFormLoadTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<IKeyInterceptorService, NoopKeyInterceptorService>();
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
    public void TheFormEditsTheModelTheReadAnswered()
    {
        var cut = Render<LoadFormHost>();

        Assert.Equal("guardado", cut.FindComponents<MudTextField<string>>()[0].Instance.Value);
        Assert.Equal(1, cut.Instance.Reads);
    }

    /// <summary>The defect itself: a failed read used to leave every field empty with Save
    /// enabled, so one click wrote blanks over the stored configuration.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AFailedReadRendersNoFieldsAndNoSave(bool throws)
    {
        var cut = Render<LoadFormHost>(p => p
            .Add(x => x.FailUntil, 1)
            .Add(x => x.Throws, throws));

        Assert.Empty(cut.FindAll("input"));
        Assert.Empty(cut.FindAll("form"));
        Assert.Empty(cut.FindAll("button[type=submit]"));

        var refusal = cut.Find(".ns-form-problem");

        Assert.Contains("La lectura no llegó.", refusal.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheRetryActRunsTheReadAgainAndTheFieldsArriveWhenItTakes()
    {
        var cut = Render<LoadFormHost>(p => p.Add(x => x.FailUntil, 1));

        await cut.InvokeAsync(() => cut.Find(".ns-form-problem-act").Click());

        Assert.Equal(2, cut.Instance.Reads);
        Assert.Empty(cut.FindAll(".ns-form-problem"));
        Assert.Equal("guardado", cut.FindComponents<MudTextField<string>>()[0].Instance.Value);
    }

    /// <summary>The third state, and the one a nullable model cannot tell from a failure: a
    /// setting nobody has saved yet is editable, because the blank is what is stored.</summary>
    [Fact]
    public void ADocumentThatDoesNotExistYetIsNotAFailure()
    {
        var cut = Render<LoadFormHost>(p => p.Add(x => x.Answer, () => new FormProblemModel()));

        Assert.Empty(cut.FindAll(".ns-form-problem"));
        Assert.Null(cut.FindComponents<MudTextField<string>>()[0].Instance.Value);
        Assert.NotEmpty(cut.FindAll("button[type=submit]"));
    }

    /// <summary>A read that answers nothing and refuses nothing has still produced no document,
    /// and rendering fields over a model nobody read is the defect — so it says so.</summary>
    [Fact]
    public void AReadThatAnswersNothingIsAFailureToo()
    {
        var cut = Render<LoadFormHost>(p => p.Add(x => x.AnswersNothing, true));

        Assert.NotEmpty(cut.FindAll(".ns-form-problem"));
        Assert.Empty(cut.FindAll("input"));
    }

    /// <summary>Master/detail: the page stays open on a document whose version just moved, so
    /// the half that owns the read is the one that goes and gets it again — no screen wires it.
    /// </summary>
    [Fact]
    public async Task ASaveThatKeepsThePageOpenReadsTheDocumentAgain()
    {
        var cut = Render<LoadFormHost>();

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.NotNull(cut.Instance.Submitted);
        Assert.Equal(2, cut.Instance.Reads);
    }
}
