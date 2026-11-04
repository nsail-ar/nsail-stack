// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>A numeric box draws spin buttons only where its value has a step: a percent is a
/// fraction, so the vendor's step of 1 would move it a hundred points, while a numeric field's
/// caller sets the Step its buttons take and a duration's figure steps by the hour.</summary>
public sealed class NsNumericSpinButtonsTests : BunitContext, IAsyncLifetime
{
    public NsNumericSpinButtonsTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton(new LanguageProvider { Current = "es" });
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
    public void A_percent_field_draws_no_spin_buttons()
    {
        var cut = Render<NsPercentField<decimal>>(ps => ps.Add(p => p.Value, 0.21m));

        Assert.Empty(cut.FindAll(".mud-input-numeric-spin"));
    }

    [Fact]
    public void A_numeric_field_steps_its_spin_buttons_by_its_step()
    {
        decimal? stepped = null;
        var cut = Render<NsNumericField<decimal>>(ps => ps
            .Add(p => p.Value, 1m)
            .Add(p => p.Step, 0.25m)
            .Add(p => p.ValueChanged, value => stepped = value));

        Assert.NotEmpty(cut.FindAll(".mud-input-numeric-spin"));

        cut.FindAll(".mud-input-numeric-spin button")[0].Click();

        Assert.Equal(1.25m, stepped);
    }

    [Fact]
    public void A_duration_field_steps_its_spin_buttons_by_an_hour()
    {
        TimeSpan? stepped = null;
        var cut = Render<NsDurationField<TimeSpan>>(ps => ps
            .Add(p => p.Value, TimeSpan.FromHours(2))
            .Add(p => p.ValueChanged, value => stepped = value));

        Assert.NotEmpty(cut.FindAll(".mud-input-numeric-spin"));

        cut.FindAll(".mud-input-numeric-spin button")[0].Click();

        Assert.Equal(TimeSpan.FromHours(3), stepped);
    }
}
