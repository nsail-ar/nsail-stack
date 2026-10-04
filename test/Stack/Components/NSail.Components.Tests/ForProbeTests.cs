// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>A field whose binding is stated as an explicit For, holding a value it was born
/// with. FieldArrivalProbeTests asks the same question of the @bind shape; this arm exists
/// because a wrapper that hands For down to the vendor gets a field-changed notification on
/// arrival, and that difference is invisible to any probe that only binds.</summary>
public sealed class ForProbeTests : BunitContext, IAsyncLifetime
{
    public ForProbeTests()
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
        return new(typeof(ForProbeTests).Assembly, Array.Empty<System.Reflection.Assembly>());
    }

    [Theory]
    [InlineData("select-bind")]
    [InlineData("select-for")]
    [InlineData("select-for-clearable")]
    [InlineData("text-for")]
    [InlineData("text-bind")]
    [InlineData("checkbox-for")]
    [InlineData("numeric-for")]
    [InlineData("money-for")]
    [InlineData("percent-for")]
    [InlineData("autocomplete-for")]
    [InlineData("textarea-for")]
    public void AnExplicitForDoesNotDirtyTheForm(string kind)
    {
        var host = Render<ForProbeHost>(p => p
            .Add(x => x.RouteTable, BuildRouteTable())
            .Add(x => x.Kind, kind));

        Assert.False(host.Instance.Surface!.HasChanges, $"{kind} went dirty on arrival");
    }
}
