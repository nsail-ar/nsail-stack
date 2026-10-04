// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;
using NSail.Problems;

namespace NSail.Components.Tests;

/// <summary>Leonardo, 2026-08-10, two reports and one root: "smtp siempre me pregunta antes de
/// navegar aunque no haya tocado nada" and "agregué dirección, guardo, sigue botón habilitado,
/// me pregunta al salir siendo que ya guardé". The baseline a tracked form measures changes
/// against has to follow the Model: a new instance is a new baseline (the form is born clean,
/// however late the read lands), and a submit the server accepted is a new baseline too (the
/// hero greys out again, leaving asks nothing). A submit the server REFUSED is neither — the
/// document is still unsaved and still says so.</summary>
public sealed class NsFormBaselineTests : BunitContext, IAsyncLifetime
{
    readonly CountingDialogManager _dialogs = new();

    public NsFormBaselineTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddComponentServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddScoped<DialogManager>(_ => _dialogs);
    }

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    static RouteTable BuildRouteTable()
    {
        return new(typeof(NsFormBaselineTests).Assembly, Array.Empty<System.Reflection.Assembly>());
    }

    IRenderedComponent<RebaseFormHost> RenderHost(Action<ComponentParameterCollectionBuilder<RebaseFormHost>>? extra = null)
    {
        return Render<RebaseFormHost>(p =>
        {
            p.Add(x => x.RouteTable, BuildRouteTable());
            extra?.Invoke(p);
        });
    }

    static bool SubmitIsDisabled(IRenderedComponent<RebaseFormHost> host)
    {
        return host.Find("button.tracked-submit").HasAttribute("disabled");
    }

    /// <summary>SmtpSettingsPage: `_edit = new()` at the field, then OnCreatedAsync awaits the
    /// read and replaces the instance the form already measured. Nobody typed, so nothing is
    /// unsaved.</summary>
    [Fact]
    public void AFormWhoseModelArrivesAfterTheFirstRender_IsBornClean()
    {
        var host = RenderHost(p => p.Add(x => x.LoadsAfterFirstRender, true));

        host.WaitForAssertion(() => Assert.Equal("smtp.stored.test", host.Instance.Model.Host));

        Assert.False(host.Instance.Surface!.HasChanges);
        Assert.True(SubmitIsDisabled(host));
    }

    [Fact]
    public async Task LeavingAFormWhoseModelArrivedLate_AsksNothing()
    {
        var host = RenderHost(p => p.Add(x => x.LoadsAfterFirstRender, true));

        host.WaitForAssertion(() => Assert.Equal("smtp.stored.test", host.Instance.Model.Host));

        var navigation = Services.GetRequiredService<NavigationManager>();

        await host.InvokeAsync(() => navigation.NavigateTo("/somewhere/else"));

        Assert.Equal(0, _dialogs.Confirms);
    }

    /// <summary>UpdatePartyPage: the handler saves, reloads into a fresh instance and stays open.
    /// The reload is the persisted state, so the form is exactly as saved — clean.</summary>
    [Fact]
    public async Task ASuccessfulSubmitThatReloadsTheModel_LeavesTheFormClean()
    {
        var host = RenderHost(p => p.Add(x => x.ReloadsOnSubmit, true));

        await host.InvokeAsync(() => host.Find("input[type=text]").Change("typed by hand"));

        Assert.True(host.Instance.Surface!.HasChanges);

        await host.InvokeAsync(() => host.Find("form").Submit());

        Assert.Equal(1, host.Instance.Submits);
        Assert.False(host.Instance.Surface!.HasChanges);
        Assert.True(SubmitIsDisabled(host));
    }

    [Fact]
    public async Task ASuccessfulSubmitThatKeepsTheSameModel_LeavesTheFormClean()
    {
        var host = RenderHost();

        await host.InvokeAsync(() => host.Find("input[type=text]").Change("typed by hand"));

        await host.InvokeAsync(() => host.Find("form").Submit());

        Assert.False(host.Instance.Surface!.HasChanges);
        Assert.True(SubmitIsDisabled(host));
    }

    [Fact]
    public async Task LeavingAfterASuccessfulSubmitThatReloaded_AsksNothing()
    {
        var host = RenderHost(p => p.Add(x => x.ReloadsOnSubmit, true));

        await host.InvokeAsync(() => host.Find("input[type=text]").Change("typed by hand"));
        await host.InvokeAsync(() => host.Find("form").Submit());

        var navigation = Services.GetRequiredService<NavigationManager>();

        await host.InvokeAsync(() => navigation.NavigateTo("/somewhere/else"));

        Assert.Equal(0, _dialogs.Confirms);
    }

    /// <summary>A Problem is not a save. The document is still unsaved, the hero still offers to
    /// send it again, and leaving still asks — the other half of the same contract (6485206e).</summary>
    [Fact]
    public async Task ASubmitTheServerRefused_KeepsTheFormDirty()
    {
        var problem = new Problem("RuleViolation", "The operation could not be completed",
            [new Issue("RuleViolation", "El puerto va de 1 a 65535.", "Port")], 422);

        var host = RenderHost(p => p.Add(x => x.Problem, problem));

        await host.InvokeAsync(() => host.Find("input[type=text]").Change("typed by hand"));

        await host.InvokeAsync(() => host.Find("form").Submit());

        host.WaitForAssertion(() => Assert.True(host.Instance.Surface!.HasChanges));
        Assert.False(SubmitIsDisabled(host));
    }

    [Fact]
    public async Task LeavingAfterARefusedSubmit_StillAsks()
    {
        var problem = new Problem("Unknown", "Something went wrong", [], 500);

        var host = RenderHost(p => p.Add(x => x.Problem, problem));

        await host.InvokeAsync(() => host.Find("input[type=text]").Change("typed by hand"));
        await host.InvokeAsync(() => host.Find("form").Submit());

        host.WaitForAssertion(() => Assert.True(host.Instance.Surface!.HasChanges));

        var navigation = Services.GetRequiredService<NavigationManager>();

        await host.InvokeAsync(() => navigation.NavigateTo("/somewhere/else"));

        Assert.Equal(1, _dialogs.Confirms);
    }

    /// <summary>The behaviour every other case is measured against, pinned so no baseline reset
    /// can swallow it: a real edit still dirties the form and still enables the hero.</summary>
    [Fact]
    public async Task AUserEdit_StillDirtiesTheFormAndEnablesTheSubmit()
    {
        var host = RenderHost(p => p.Add(x => x.LoadsAfterFirstRender, true));

        host.WaitForAssertion(() => Assert.Equal("smtp.stored.test", host.Instance.Model.Host));

        await host.InvokeAsync(() => host.Find("input[type=text]").Change("smtp.typed.test"));

        Assert.True(host.Instance.Surface!.HasChanges);
        Assert.False(SubmitIsDisabled(host));
    }
}
