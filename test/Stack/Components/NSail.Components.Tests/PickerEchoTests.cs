// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;
using static NSail.Components.Tests.Fixtures.PickerNotifyHost;

namespace NSail.Components.Tests;

/// <summary>nsail#2891: PickerNotifyTests' echo, one step further out. A value the page pushed
/// into a picker is the page's own, and the picker hands it back as nothing at all — no field
/// notification and no ValueChanged — so a page that reads on every change of its field (the
/// Calendario's Desde, which its arrows move) reads once for a move of its own. What a person
/// picks or types still reaches the page once, a pick back to the page's own value inside the
/// first pick's round trip included.</summary>
public sealed class PickerEchoTests : BunitContext, IAsyncLifetime
{
    public PickerEchoTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddComponentServices();
        Services.AddScoped<IKeyInterceptorService, NoopKeyInterceptorService>();
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

    [Theory]
    [InlineData(PickerKind.Date)]
    [InlineData(PickerKind.Time)]
    [InlineData(PickerKind.DateTime)]
    public async Task AValueThePagePushesInRaisesNoValueChangedAndNoNotification(PickerKind picker)
    {
        var host = Render<PickerNotifyHost>(p => p
            .Add(x => x.Model, new FieldProbeModel())
            .Add(x => x.Picker, picker));

        await host.InvokeAsync(host.Instance.FillInPlace);

        // The vendor's date event is debounced; an echo that would land, lands inside this.
        await Task.Delay(400);

        Assert.Equal(0, host.Instance.Changes);
        Assert.Equal(0, host.Instance.Notifications);
    }

    [Theory]
    [InlineData(PickerKind.Date)]
    [InlineData(PickerKind.Time)]
    [InlineData(PickerKind.DateTime)]
    public async Task APersonsChangeRaisesValueChangedAndANotificationOnce(PickerKind picker)
    {
        var host = Render<PickerNotifyHost>(p => p
            .Add(x => x.Model, new FieldProbeModel())
            .Add(x => x.Picker, picker));

        await host.InvokeAsync(host.Instance.FillInPlace);
        await Task.Delay(400);

        await Pick(host, picker);
        await Task.Delay(400);

        Assert.Equal(1, host.Instance.Changes);
        Assert.Equal(1, host.Instance.Notifications);
    }

    // Two picks inside one round trip: the page still holds its own date when the second pick
    // names it back, and that pick is the person's, not an echo. The host here applies nothing,
    // which is the window the round trip leaves open.
    [Fact]
    public async Task APickBackToThePagesDateInsideTheFirstPicksRoundTripStillReachesThePage()
    {
        var raised = new List<DateOnly?>();

        var cut = Render<NsDateField<DateOnly?>>(ps => ps
            .Add(p => p.Value, new DateOnly(2026, 11, 2))
            .Add(p => p.ValueChanged, raised.Add));

        await cut.InvokeAsync(() => cut.Find("input").Change("11/16/2026"));
        await cut.InvokeAsync(() => cut.Find("input").Change("11/02/2026"));

        cut.WaitForAssertion(() => Assert.Equal([new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 2)], raised));
    }

    // The vendor's own callbacks, raised the way a pick raises them: the date box is left with a
    // typed date, the time picker hands its time over.
    static Task Pick(IRenderedComponent<PickerNotifyHost> host, PickerKind picker)
    {
        return picker switch
        {
            PickerKind.Time => host.InvokeAsync(() => host.FindComponent<MudTimePicker>().Instance.TimeChanged.InvokeAsync(new TimeSpan(9, 15, 0))),
            PickerKind.DateTime => host.InvokeAsync(() => host.FindComponent<MudDatePicker>().Find("input").Change("05/12/2026")),
            _ => host.InvokeAsync(() => host.Find("input").Change("05/12/2026")),
        };
    }
}
