// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;

namespace NSail.Components.Tests;

/// <summary>A duration past TimeSpan's range is refused on the field, and a password box carries
/// an autofill token without being told.</summary>
public sealed class NsFieldHardeningTests : BunitContext, IAsyncLifetime
{
    public NsFieldHardeningTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddStringsFromAssembly(typeof(NsPasswordField).Assembly);
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
    public async Task ADurationBeyondTheRangeIsRefusedNotThrown()
    {
        TimeSpan? held = TimeSpan.FromHours(2);
        var cut = Render<NsDurationField<TimeSpan?>>(ps => ps
            .Add(p => p.Label, "Ventana")
            .Add(p => p.Value, held)
            .Add(p => p.ValueChanged, v => held = v));

        await cut.InvokeAsync(() => cut.Find("input").Input("999999999"));

        Assert.Equal(TimeSpan.FromHours(2), held);
        Assert.Contains("mud-input-error", cut.Markup);
    }

    [Fact]
    public async Task ADurationThePageWritesLiftsTheRefusal()
    {
        var model = new DurationFormHost.FormModel { Length = TimeSpan.FromHours(2) };
        var submitted = false;
        var cut = Render<DurationFormHost>(ps => ps
            .Add(p => p.Model, model)
            .Add(p => p.Submitted, () => submitted = true));

        await cut.InvokeAsync(() => cut.Find("input").Input("999999999"));
        await cut.InvokeAsync(() => cut.Instance.Write(TimeSpan.FromHours(3)));
        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.True(submitted);
        Assert.Empty(cut.FindAll(".mud-input-error"));
        Assert.Equal(TimeSpan.FromHours(3), model.Length);
    }

    [Fact]
    public async Task AValidDurationStillCommits()
    {
        TimeSpan? held = null;
        var cut = Render<NsDurationField<TimeSpan?>>(ps => ps
            .Add(p => p.Label, "Ventana")
            .Add(p => p.ValueChanged, v => held = v));

        await cut.InvokeAsync(() => cut.Find("input").Input("3"));

        Assert.Equal(TimeSpan.FromHours(3), held);
    }

    [Theory]
    [InlineData(false, false, null, "current-password")]
    [InlineData(true, false, null, "new-password")]
    [InlineData(false, true, null, "new-password")]
    [InlineData(false, false, "off", "off")]
    [InlineData(false, true, "off", "off")]
    public void ThePasswordBoxDefaultsItsAutofillToken(bool isNew, bool isStored, string? given, string expected)
    {
        var cut = Render<NsPasswordField>(ps => ps
            .Add(p => p.Label, "Secret")
            .Add(p => p.IsNew, isNew)
            .Add(p => p.IsStored, isStored)
            .Add(p => p.Autocomplete, given)
            .Add(p => p.Value, "x"));

        Assert.Equal(expected, cut.Find("input").GetAttribute("autocomplete"));
    }
}
