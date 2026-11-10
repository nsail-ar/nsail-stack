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

public sealed class NsTimeFieldTests : BunitContext, IAsyncLifetime
{
    public NsTimeFieldTests()
    {
        Services.AddMudServices();
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
    public void ATimeOnlyIsHeldAsTheSameTimeOfDay()
    {
        var cut = Render<NsTimeField<TimeOnly>>(ps => ps.Add(p => p.Value, new TimeOnly(9, 30)));

        Assert.Equal(new TimeSpan(9, 30, 0), cut.FindComponent<MudTimePicker>().Instance.Time);
    }

    [Fact]
    public async Task PickingATimeHandsBackTheSameTypeTheFieldIsBoundTo()
    {
        TimeOnly? picked = null;
        TimeSpan? span = null;

        var only = Render<NsTimeField<TimeOnly>>(ps => ps.Add(p => p.ValueChanged, (TimeOnly value) => picked = value));
        var spans = Render<NsTimeField<TimeSpan?>>(ps => ps.Add(p => p.ValueChanged, (TimeSpan? value) => span = value));

        await only.InvokeAsync(() => only.FindComponent<MudTimePicker>().Instance.TimeChanged.InvokeAsync(new TimeSpan(14, 5, 0)));
        await spans.InvokeAsync(() => spans.FindComponent<MudTimePicker>().Instance.TimeChanged.InvokeAsync(new TimeSpan(7, 0, 0)));

        Assert.Equal(new TimeOnly(14, 5), picked);
        Assert.Equal(new TimeSpan(7, 0, 0), span);
    }

    [Fact]
    public async Task ClearingTheTimeHandsBackNull()
    {
        TimeSpan? span = new TimeSpan(1, 0, 0);

        var cut = Render<NsTimeField<TimeSpan?>>(ps => ps
            .Add(p => p.Value, span)
            .Add(p => p.ValueChanged, (TimeSpan? value) => span = value));

        await cut.InvokeAsync(() => cut.FindComponent<MudTimePicker>().Instance.TimeChanged.InvokeAsync(null));

        Assert.Null(span);
    }
}
