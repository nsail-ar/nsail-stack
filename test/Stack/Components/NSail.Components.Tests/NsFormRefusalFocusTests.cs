// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;
using NSail.Problems;

namespace NSail.Components.Tests;

/// <summary>nsail#4199: a refused submit hands the cursor to the first field in error, so a
/// refusal below the fold is not a press that seems to do nothing. The form asks the browser to
/// move once the marks are drawn (nsapp.focusRefused); a refusal with no field marked, and a
/// submit that took, ask nothing.</summary>
public sealed class NsFormRefusalFocusTests : BunitContext, IAsyncLifetime
{
    const string Signal = "nsapp.focusRefused";

    public NsFormRefusalFocusTests()
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

    IReadOnlyList<JSRuntimeInvocation> Calls => [.. JSInterop.Invocations[Signal]];

    static Problem Invalid(string source)
    {
        return new("InvalidModel", "One or more fields contain invalid data", [new Issue("Required", "Obligatorio", source)], 400);
    }

    static void AssertMovedTo<TComponent>(IRenderedComponent<TComponent> cut, IReadOnlyList<JSRuntimeInvocation> calls)
        where TComponent : class, Microsoft.AspNetCore.Components.IComponent
    {
        var call = Assert.Single(calls);

        Assert.Equal(cut.Find("form").Id, call.Arguments[0]);
    }

    [Fact]
    public async Task ARequiredFieldLeftEmpty_MovesTheCursorToTheFirstFieldInError()
    {
        var model = new RequiredFieldsModel { Name = null, PatientId = Guid.NewGuid(), CategoryId = Guid.NewGuid() };

        var cut = Render<RequiredFieldsHost>(p => p
            .Add(x => x.Model, model)
            .Add(x => x.Submitted, () => { }));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        cut.WaitForAssertion(() => AssertMovedTo(cut, Calls));
    }

    [Fact]
    public async Task AServerRefusalPlacedAtAField_MovesTheCursorToIt()
    {
        var cut = Render<FormProblemHost>(p => p
            .Add(x => x.Model, new FormProblemModel())
            .Add(x => x.Problem, Invalid(nameof(FormProblemModel.Alias))));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        cut.WaitForAssertion(() => AssertMovedTo(cut, Calls));
    }

    [Fact]
    public async Task AServerRefusalAfterARealWait_MovesTheCursorOnceTheFieldsTakeIt()
    {
        var running = new TaskCompletionSource();
        var frozen = new List<bool>();
        IRenderedComponent<FormProblemHost>? cut = null;

        // Read at the moment of the call: a field frozen by the save in flight takes no cursor.
        JSInterop.SetupVoid(Signal, _ =>
        {
            frozen.Add(cut!.FindAll("input").Any(input => input.HasAttribute("disabled")));

            return true;
        }).SetVoidResult();

        cut = Render<FormProblemHost>(p => p
            .Add(x => x.Model, new FormProblemModel())
            .Add(x => x.Problem, Invalid(nameof(FormProblemModel.Alias)))
            .Add(x => x.Hold, running.Task));

        // Discarded on purpose: the submit only completes once `running` is released below.
        _ = cut.InvokeAsync(() => cut.Find("form").Submit());

        cut.WaitForAssertion(() => Assert.Contains(cut.FindAll("input"), input => input.HasAttribute("disabled")));

        running.SetResult();

        cut.WaitForAssertion(() => AssertMovedTo(cut, Calls));

        Assert.Equal([false], frozen);
    }

    [Fact]
    public async Task ARefusalDrawnOnlyAtTheFoot_MovesNothing()
    {
        var cut = Render<FormProblemHost>(p => p
            .Add(x => x.Model, new FormProblemModel())
            .Add(x => x.Problem, Invalid(nameof(FormProblemModel.Nickname))));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.NotEmpty(cut.FindAll(".ns-form-problem"));
        Assert.Empty(Calls);
    }

    [Fact]
    public async Task ASubmitThatTakes_MovesNothing()
    {
        var model = new RequiredFieldsModel { Name = "Ana", PatientId = Guid.NewGuid(), CategoryId = Guid.NewGuid() };
        var submitted = false;

        var cut = Render<RequiredFieldsHost>(p => p
            .Add(x => x.Model, model)
            .Add(x => x.Submitted, () => submitted = true));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.True(submitted);
        Assert.Empty(Calls);
    }

    [Fact]
    public async Task AFormInADialog_MovesTheCursorToItsFieldInError()
    {
        Services.AddComponentServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();

        var host = Render<DialogHostFixture>();
        var dialogs = Services.GetRequiredService<DialogManager>();

        // Open completes when the dialog CLOSES, so awaiting it here would hang the test.
        _ = host.InvokeAsync(() => dialogs.Open<DialogRefusalProbeBody>("Dialog title"));

        host.WaitForAssertion(() => Assert.Single(host.FindAll("form")));

        await host.InvokeAsync(() => host.Find("form").Submit());

        host.WaitForAssertion(() => AssertMovedTo(host, Calls));
    }
}
