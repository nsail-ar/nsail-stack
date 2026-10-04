// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>Dispatch, 2026-08-03: a production report on GoogleSettingsPage suspected
/// NsCheckBox of the same defect family 3df66c2f fixed for PolicyAudienceEditor/
/// PolicyMessagesEditor — a toggle whose change never reaches EditContext, leaving
/// NsSubmit disabled or letting a mixed edit save a stale model. NsCheckBox already
/// inherits NsFieldBase and routes through SetValue -> EditContext.NotifyFieldChanged,
/// the same shape PolicyConstraintsEditor always had; live verification on dev1 (port
/// 5110, GoogleSettingsPage) confirmed a toggle-only edit and a secret+toggle combo both
/// enable Save and persist correctly, so the suspicion did not hold. No test pinned this
/// component-level path before, so it lands here rather than staying an unverified
/// suspicion the next screen could reopen.</summary>
public sealed class NsCheckBoxDirtyTrackingTests : BunitContext, IAsyncLifetime
{
    public NsCheckBoxDirtyTrackingTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddComponentServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
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
        return new(typeof(NsCheckBoxDirtyTrackingTests).Assembly, Array.Empty<System.Reflection.Assembly>());
    }

    [Fact]
    public async Task TogglingTheCheckBoxAlone_MarksTheSurfaceDirtyAndEnablesSubmit()
    {
        var host = Render<CheckBoxTrackingHost>(p => p
            .Add(x => x.RouteTable, BuildRouteTable())
            .Add(x => x.Model, new CheckBoxTrackingModel()));

        Assert.False(host.Instance.Surface!.HasChanges);
        Assert.True(host.Find("button.tracked-submit").HasAttribute("disabled"));

        await host.InvokeAsync(() => host.Find("input[type=checkbox]").Change(true));

        Assert.True(host.Instance.Surface!.HasChanges);
        Assert.False(host.Find("button.tracked-submit").HasAttribute("disabled"));
    }

    [Fact]
    public async Task SubmittingAToggleOnlyEdit_CarriesTheNewValueOnTheWire()
    {
        CheckBoxTrackingModel? submitted = null;

        var host = Render<CheckBoxTrackingHost>(p => p
            .Add(x => x.RouteTable, BuildRouteTable())
            .Add(x => x.Model, new CheckBoxTrackingModel())
            .Add(x => x.Submitted, m => submitted = m));

        await host.InvokeAsync(() => host.Find("input[type=checkbox]").Change(true));
        await host.InvokeAsync(() => host.Find("form").Submit());

        Assert.NotNull(submitted);
        Assert.True(submitted!.Flag);
    }

    [Fact]
    public async Task SubmittingATogglePlusTextEdit_CarriesBothValuesOnTheWire()
    {
        CheckBoxTrackingModel? submitted = null;

        var host = Render<CheckBoxTrackingHost>(p => p
            .Add(x => x.RouteTable, BuildRouteTable())
            .Add(x => x.Model, new CheckBoxTrackingModel())
            .Add(x => x.Submitted, m => submitted = m));

        await host.InvokeAsync(() => host.Find("input[type=checkbox]").Change(true));
        await host.InvokeAsync(() => host.Find("input[type=text]").Change("placeholder-secret"));
        await host.InvokeAsync(() => host.Find("form").Submit());

        Assert.NotNull(submitted);
        Assert.True(submitted!.Flag);
        Assert.Equal("placeholder-secret", submitted.Secret);
    }

    [Fact]
    public async Task TogglingTheCheckBoxInAnUntrackedForm_NeverMarksTheSurfaceDirty()
    {
        var host = Render<CheckBoxTrackingHost>(p => p
            .Add(x => x.RouteTable, BuildRouteTable())
            .Add(x => x.Model, new CheckBoxTrackingModel())
            .Add(x => x.Untracked, true));

        await host.InvokeAsync(() => host.Find("input[type=checkbox]").Change(true));

        Assert.False(host.Instance.Surface!.HasChanges);
    }
}
