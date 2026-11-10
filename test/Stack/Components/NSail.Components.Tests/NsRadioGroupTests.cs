// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

public sealed class NsRadioGroupTests : BunitContext, IAsyncLifetime
{
    sealed record Plan(int Id, string Name);

    static readonly Plan[] Plans = [new(1, "Basic"), new(2, "Pro")];

    public NsRadioGroupTests()
    {
        Services.AddMudServices();
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

    IRenderedComponent<NsRadioGroup<Plan, int>> Group(Action<ComponentParameterCollectionBuilder<NsRadioGroup<Plan, int>>>? more = null)
    {
        return Render<NsRadioGroup<Plan, int>>(ps =>
        {
            ps.Add(p => p.Label, "Plan")
                .Add(p => p.Items, Plans)
                .Add(p => p.ItemValue, plan => plan.Id)
                .Add(p => p.ItemText, plan => plan.Name)
                .Add(p => p.Value, 1);

            more?.Invoke(ps);
        });
    }

    [Fact]
    public void EachItemIsAnOptionLabelledByItsText()
    {
        var cut = Group();

        Assert.Equal(2, cut.FindAll("input[type=radio]").Count);
        Assert.Contains("Basic", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Pro", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void TheHeldValueIsTheOneChecked()
    {
        var cut = Group();

        var checkedOnes = cut.FindAll("input[type=radio]").Where(input => input.HasAttribute("checked")).ToList();

        Assert.Single(checkedOnes);
        Assert.Equal("Basic", cut.FindAll("label").First(label => label.QuerySelector("input[checked]") is not null).TextContent.Trim());
    }

    [Fact]
    public async Task ChoosingAnotherOptionRaisesValueChangedWithItsValue()
    {
        var chosen = 0;
        var cut = Group(ps => ps.Add(p => p.ValueChanged, (int value) => chosen = value));

        await cut.InvokeAsync(() => cut.FindComponent<MudBlazor.MudRadioGroup<int>>().Instance.ValueChanged.InvokeAsync(2));

        Assert.Equal(2, chosen);
    }

    [Fact]
    public void ADisabledGroupDisablesEveryOption()
    {
        var cut = Group(ps => ps.Add(p => p.Disabled, true));

        Assert.All(cut.FindAll("input[type=radio]"), input => Assert.True(input.HasAttribute("disabled")));
    }

    [Fact]
    public void WithoutItemsNoOptionIsDrawn()
    {
        var cut = Render<NsRadioGroup<Plan, int>>(ps => ps.Add(p => p.Label, "Plan"));

        Assert.Empty(cut.FindAll("input[type=radio]"));
    }

    [Fact]
    public void WithoutAnItemTextTheItemsOwnStringIsTheLabel()
    {
        var cut = Render<NsRadioGroup<string, string>>(ps => ps
            .Add(p => p.Label, "Pick")
            .Add(p => p.Items, ["red", "green"]));

        Assert.Contains("red", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("green", cut.Markup, StringComparison.Ordinal);
    }
}
