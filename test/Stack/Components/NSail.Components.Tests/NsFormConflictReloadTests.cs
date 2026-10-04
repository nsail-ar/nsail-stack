// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;
using NSail.Problems;

namespace NSail.Components.Tests;

/// <summary>nsail#197 at the screen: a save refused because the row moved underneath must not
/// cost the user the edit they were making. The values stay in the fields, the refusal draws
/// where every other refusal draws, and the only new thing is one act — go and get the newer
/// row — which the user chooses, never the form.</summary>
public sealed class NsFormConflictReloadTests : BunitContext, IAsyncLifetime
{
    public NsFormConflictReloadTests()
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

    static Problem Conflict()
    {
        return BusinessProblem.Conflict("Setting");
    }

    [Fact]
    public async Task AConflictKeepsTheTypedValuesAndDrawsTheRefusalInline()
    {
        var model = new FormProblemModel();

        var cut = Render<FormProblemHost>(p => p
            .Add(x => x.Model, model)
            .Add(x => x.Problem, Conflict()));

        var fields = cut.FindComponents<MudTextField<string>>();

        await cut.InvokeAsync(() => fields[0].Find("input").Change("lo que el usuario escribió"));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        // The edit is untouched — in the model the form holds, and on the screen the user is
        // looking at. Anything less and the refusal costs more than the save it refused.
        Assert.Equal("lo que el usuario escribió", model.Name);
        Assert.Equal(
            "lo que el usuario escribió",
            cut.FindComponents<MudTextField<string>>()[0].Instance.Value);

        var alert = cut.Find(".ns-form-problem");

        Assert.Contains("was modified by another process", alert.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AConflictOffersTheReloadAct_AndInvokingItAsksTheScreenToRefetch()
    {
        var reloads = 0;

        var cut = Render<FormProblemHost>(p => p
            .Add(x => x.Model, new FormProblemModel())
            .Add(x => x.Problem, Conflict())
            .Add(x => x.Reloaded, () => reloads++));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        await cut.InvokeAsync(() => cut.Find(".ns-form-problem-act").Click());

        Assert.Equal(1, reloads);
    }

    /// <summary>The act belongs to this one refusal. Every other "no" is answered by fixing the
    /// form, and offering to throw the screen's state away there would be an invitation to lose
    /// work for no reason.</summary>
    [Fact]
    public async Task ARefusalThatIsNotAConflictOffersNoReloadAct()
    {
        var cut = Render<FormProblemHost>(p => p
            .Add(x => x.Model, new FormProblemModel())
            .Add(x => x.Problem, BusinessProblem.RuleViolation("AxisRange", "El eje va de 1 a 180 grados."))
            .Add(x => x.Reloaded, () => { }));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.NotEmpty(cut.FindAll(".ns-form-problem"));
        Assert.Empty(cut.FindAll(".ns-form-problem-act"));
    }

    /// <summary>And it goes when the refusal goes: a later submit the server accepted leaves no
    /// act pointing at a conflict that is over.</summary>
    [Fact]
    public async Task ASubmitWithNoProblemTakesTheReloadActAway()
    {
        var cut = Render<FormProblemHost>(p => p
            .Add(x => x.Model, new FormProblemModel())
            .Add(x => x.Problem, Conflict())
            .Add(x => x.Reloaded, () => { }));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.NotEmpty(cut.FindAll(".ns-form-problem-act"));

        cut.Render(p => p.Add(x => x.Problem, (Problem?)null));
        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.Empty(cut.FindAll(".ns-form-problem-act"));
    }
}
