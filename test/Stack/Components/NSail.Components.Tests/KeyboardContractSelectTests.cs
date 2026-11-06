// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>The keyboard contract of a closed list (keyboard.md): the box is a tab stop and a
/// combobox. The vendor reads Arrow Down, Enter, Space and Esc through its JS key interceptor,
/// which bUnit cannot drive, so those keys are the browser walk's (keyboard.md, *Walked in the
/// browser*).</summary>
public sealed class KeyboardContractSelectTests : BunitContext, IAsyncLifetime
{
    public KeyboardContractSelectTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(KeyboardContractSelectTests).Assembly, []));
        Services.AddScoped<RootSurface>();
        Services.AddScoped<SurfaceHistory>();
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

    static readonly SelectRef Alpha = new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alpha");
    static readonly SelectRef Beta = new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Beta");

    IRenderedComponent<SelectValueHost<SelectRef, Guid>> Mount()
    {
        return Render<SelectValueHost<SelectRef, Guid>>(ps => ps
            .Add(p => p.Items, new[] { Alpha, Beta })
            .Add(p => p.ItemValue, item => item.Id)
            .Add(p => p.ItemText, item => item.DisplayName)
            .Add(p => p.Label, "Recurso"));
    }

    [Fact]
    public void TheBoxIsATabStopWithTheComboboxRole()
    {
        var input = Mount().Find("input.mud-select-input");

        Assert.Equal("combobox", input.GetAttribute("role"));
        Assert.NotEqual("-1", input.GetAttribute("tabindex"));
        Assert.Null(input.GetAttribute("disabled"));
    }
}
