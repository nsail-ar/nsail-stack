// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>The money field carries its own bounds so a rule the caller already knows is said
/// on the field before any round trip (nsail#815, Autorizar's covered amount). The knob is the
/// vendor's own Min/Max, reached the way Step already is — an assignment sentinel, never a null
/// check, because TValue may be a non-nullable numeric with no null to compare against — so a
/// field nobody bounded keeps the vendor's default and renders no bound at all.</summary>
public sealed class NsMoneyFieldBoundsTests : BunitContext, IAsyncLifetime
{
    public NsMoneyFieldBoundsTests()
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

    [Fact]
    public void A_bounded_field_carries_both_bounds_on_its_input()
    {
        var cut = Render<NsMoneyField<decimal?>>(ps => ps
            .Add(p => p.Value, 0m)
            .Add(p => p.Min, 0m)
            .Add(p => p.Max, 30000m));

        var input = cut.Find("input");

        Assert.Equal("0", input.GetAttribute("min"));
        Assert.Equal("30000", input.GetAttribute("max"));
    }

    [Fact]
    public void An_unbounded_field_carries_neither()
    {
        var cut = Render<NsMoneyField<decimal?>>(ps => ps.Add(p => p.Value, 0m));

        var input = cut.Find("input");

        Assert.Null(input.GetAttribute("min"));
        Assert.Null(input.GetAttribute("max"));
    }

    // One bound is a bound: a floor with no ceiling reaches the input alone.
    [Fact]
    public void A_floor_with_no_ceiling_reaches_the_input_alone()
    {
        var cut = Render<NsMoneyField<decimal?>>(ps => ps
            .Add(p => p.Value, 0m)
            .Add(p => p.Min, 0m));

        var input = cut.Find("input");

        Assert.Equal("0", input.GetAttribute("min"));
        Assert.Null(input.GetAttribute("max"));
    }

    // The vendor clamps what is typed past the ceiling, so the bound is not only advice the
    // browser draws — the value the field hands back never leaves the range.
    [Fact]
    public async Task A_figure_over_the_ceiling_is_clamped_to_it()
    {
        decimal? held = null;

        var cut = Render<NsMoneyField<decimal?>>(ps => ps
            .Add(p => p.Value, 0m)
            .Add(p => p.Min, 0m)
            .Add(p => p.Max, 30000m)
            .Add(p => p.ValueChanged, v => held = v));

        await cut.Find("input").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "40000" });

        Assert.Equal(30000m, held);
    }
}
