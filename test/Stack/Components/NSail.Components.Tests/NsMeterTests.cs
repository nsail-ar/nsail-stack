// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace NSail.Components.Tests;

public sealed class NsMeterTests : BunitContext, IAsyncLifetime
{
    public NsMeterTests()
    {
        Services.AddMudServices();
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

    MudProgressLinear Bar(double value, double max = 100, NsSeverity? severity = null)
    {
        var cut = Render<NsMeter>(ps =>
        {
            ps.Add(p => p.Value, value).Add(p => p.Max, max);

            if (severity is { } tone)
            {
                ps.Add(p => p.Severity, tone);
            }
        });

        return cut.FindComponent<MudProgressLinear>().Instance;
    }

    [Theory]
    [InlineData(25, 100, 25)]
    [InlineData(50, 200, 25)]
    [InlineData(0, 10, 0)]
    public void TheBarShowsTheShareOfTheAllowanceSpent(double value, double max, double percent)
    {
        Assert.Equal(percent, Bar(value, max).Value);
    }

    [Fact]
    public void AReadingPastTheCeilingDrawsAFullBar()
    {
        Assert.Equal(100, Bar(150, 100).Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void ANoAllowanceDrawsAnEmptyBar(double max)
    {
        Assert.Equal(0, Bar(10, max).Value);
    }

    [Fact]
    public void NoSeverityIsThePrimaryAccent()
    {
        Assert.Equal(Color.Primary, Bar(10).Color);
    }

    [Theory]
    [InlineData(NsSeverity.Error, Color.Error)]
    [InlineData(NsSeverity.Warning, Color.Warning)]
    [InlineData(NsSeverity.Success, Color.Success)]
    [InlineData(NsSeverity.Info, Color.Info)]
    public void EachSeverityDrawsItsOwnColor(NsSeverity severity, Color color)
    {
        Assert.Equal(color, Bar(100, 100, severity).Color);
    }
}
