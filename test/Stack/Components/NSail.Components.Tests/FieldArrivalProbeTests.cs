// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

public sealed class FieldArrivalProbeTests : BunitContext, IAsyncLifetime
{
    public FieldArrivalProbeTests()
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
        return new(typeof(FieldArrivalProbeTests).Assembly, Array.Empty<System.Reflection.Assembly>());
    }

    public static TheoryData<string, string> Kinds()
    {
        string[] kinds =
        [
            "text", "password", "textarea", "email", "phone", "url", "color",
            "numeric", "money", "percent", "duration", "date", "dateonly", "time",
            "datetime", "select", "select-required", "autocomplete", "checkbox"
        ];

        string[] modes = ["empty", "preset", "load-swap", "load-inplace"];

        var data = new TheoryData<string, string>();

        foreach (var kind in kinds)
        {
            foreach (var mode in modes)
            {
                data.Add(kind, mode);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void AFieldIsNotDirtyOnArrival(string kind, string mode)
    {
        var host = Render<FieldProbeHost>(p => p
            .Add(x => x.RouteTable, BuildRouteTable())
            .Add(x => x.Kind, kind)
            .Add(x => x.Mode, mode));

        host.WaitForAssertion(() => Assert.True(host.Instance.Loaded));

        Assert.False(host.Instance.Surface!.HasChanges, $"{kind}/{mode} went dirty on arrival");
    }
}
