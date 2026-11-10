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

    MudProgressLinear Bar(double value, double max = 100, NsAs? meaning = null)
    {
        var cut = Render<NsMeter>(ps =>
        {
            ps.Add(p => p.Value, value).Add(p => p.Max, max);

            if (meaning is { } as_)
            {
                ps.Add(p => p.As, as_);
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
    public void AnUnreadMeaningIsThePrimaryAccent()
    {
        Assert.Equal(Color.Primary, Bar(10).Color);
    }

    [Fact]
    public void DangerSaysTheAllowanceIsSpentAsAWarning()
    {
        Assert.Equal(Color.Warning, Bar(100, 100, NsAs.Danger).Color);
    }

    [Fact]
    public void TheDefaultMeaningIsInfo()
    {
        Assert.Equal(Color.Info, Bar(10, 100, NsAs.Default).Color);
    }
}
