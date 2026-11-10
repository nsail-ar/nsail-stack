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

public sealed class NsConfirmDialogTests : BunitContext, IAsyncLifetime
{
    public NsConfirmDialogTests()
    {
        Services.AddMudServices();
        Services.AddScoped<IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            ["Common.Yes"] = "Yes",
            ["Common.No"] = "No",
        })]));
        Services.AddSingleton(new LanguageProvider { Current = "en" });
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddScoped<DialogManager, MudDialogManager>();
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

    async Task<(Task<bool> Answer, IRenderedComponent<MudDialogProvider> Provider)> Ask(bool destroys)
    {
        var provider = Render<MudDialogProvider>();
        var dialogs = Services.GetRequiredService<DialogManager>();

        var answer = destroys ? dialogs.ConfirmDestruction("Sure?", "Title") : dialogs.Confirm("Sure?", "Title");

        await provider.WaitForAssertionAsync(() => Assert.NotEmpty(provider.FindAll("button")));

        return (answer, provider);
    }

    [Fact]
    public async Task TheQuestionShowsItsMessageAndOffersNoThenYes()
    {
        var (_, provider) = await Ask(destroys: false);

        Assert.Contains("Sure?", provider.Markup, StringComparison.Ordinal);
        Assert.Equal(["No", "Yes"], provider.FindAll(".mud-dialog-actions button").Select(button => button.TextContent.Trim()).ToArray());
    }

    [Fact]
    public async Task YesAnswersTrue()
    {
        var (answer, provider) = await Ask(destroys: false);

        await provider.InvokeAsync(() => provider.FindAll(".mud-dialog-actions button")[1].Click());

        Assert.True(await answer.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task NoAnswersFalse()
    {
        var (answer, provider) = await Ask(destroys: false);

        await provider.InvokeAsync(() => provider.FindAll(".mud-dialog-actions button")[0].Click());

        Assert.False(await answer.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task ADestructiveQuestionDrawsTheAffirmativeFaceAsError()
    {
        var (_, provider) = await Ask(destroys: true);

        var yes = provider.FindAll(".mud-dialog-actions button")[1];

        Assert.Contains("mud-button-filled-error", yes.ClassName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnOrdinaryQuestionKeepsTheAccentAsText()
    {
        var (_, provider) = await Ask(destroys: false);

        var yes = provider.FindAll(".mud-dialog-actions button")[1];

        Assert.Contains("mud-button-text-primary", yes.ClassName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheCursorOpensOnTheWayOutForADestructiveQuestionAndOnTheActOtherwise()
    {
        var (_, destroying) = await Ask(destroys: true);

        var (_, ordinary) = await Ask(destroys: false);

        Assert.Equal(DefaultFocus.FirstChild, destroying.FindComponent<MudDialog>().Instance.DefaultFocus);
        Assert.Equal(DefaultFocus.LastChild, ordinary.FindComponent<MudDialog>().Instance.DefaultFocus);
    }
}
