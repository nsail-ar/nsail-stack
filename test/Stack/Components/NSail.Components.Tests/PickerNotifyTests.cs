// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>The false dirty at the seam it is actually made on. MudBlazor's pickers raise
/// their *Changed callback when a value is pushed INTO them from the model, not only when
/// someone picks one — measured here, this host counted 1 notification for a value nobody
/// typed before NsFieldBase learned to tell an echo from an edit. The form's own arrival
/// tests (FieldArrivalProbeTests) show what that cost the user; this one shows why.</summary>
public sealed class PickerNotifyTests : BunitContext, IAsyncLifetime
{
    public PickerNotifyTests()
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

    [Fact]
    public async Task FillingTheModelInPlace_RaisesNoFieldNotification()
    {
        var model = new FieldProbeModel();

        var host = Render<PickerNotifyHost>(p => p.Add(x => x.Model, model));

        Assert.Equal(0, host.Instance.Notifications);

        await host.InvokeAsync(host.Instance.FillInPlace);

        // The picker still echoed — the value reached it and it called back — but the echo
        // carried the value the field already displayed, so nothing was reported as changed.
        Assert.Equal(0, host.Instance.Notifications);
        Assert.Equal(new DateOnly(2026, 4, 8), model.DateOnlyValue);
    }

    // The other half of the rule, and the one that keeps the cure from being "never report
    // anything": a date the person actually types is a change, and Save has to light up.
    [Fact]
    public async Task TypingADate_RaisesAFieldNotification()
    {
        var model = new FieldProbeModel();

        var host = Render<PickerNotifyHost>(p => p.Add(x => x.Model, model));

        // Unmasked, the vendor keeps its own change wiring: what is typed belongs to the
        // browser until the field is left, and leaving it is the one event that carries the
        // whole text into .NET (nsail#1196 — the mask that drove every keystroke through
        // oninput is what lost them under load).
        await host.InvokeAsync(() => host.Find("input").Change("08/04/2026"));

        Assert.Equal(1, host.Instance.Notifications);
        Assert.Equal("DateOnlyValue", Assert.Single(host.Instance.Fields));

        // Which calendar day the typed string means is the picker culture's business, and
        // NsDateTimeFieldTests owns that question — here it only has to have landed.
        Assert.NotEqual(default, model.DateOnlyValue);
    }

    [Fact]
    public async Task SwappingTheModelInstance_RaisesNoFieldNotification()
    {
        var host = Render<PickerNotifyHost>(p => p.Add(x => x.Model, new FieldProbeModel()));

        await host.InvokeAsync(host.Instance.Swap);

        Assert.Equal(0, host.Instance.Notifications);
    }
}
