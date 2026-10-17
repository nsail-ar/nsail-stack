// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#2338: AutoFocus is one source of the router's arrival marker (NsArrivalFocus,
/// ns.js) — pinned here as the markup half, since there is no JS harness in the tree
/// (NsFocusCandidateMarkTests' own note) to prove the DOM precedence the marker is read by.</summary>
public sealed class NsArrivalFocusMarkTests : BunitContext, IAsyncLifetime
{
    public NsArrivalFocusMarkTests()
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
    public void AnAutoFocusField_CarriesTheArrivalMark()
    {
        var cut = Render<NsTextField>(p => p
            .Add(x => x.Label, "Código")
            .Add(x => x.AutoFocus, true));

        Assert.Equal("true", cut.Find("input").GetAttribute("data-ns-arrival-focus"));
    }

    [Fact]
    public void APlainField_CarriesNoArrivalMark()
    {
        var cut = Render<NsTextField>(p => p.Add(x => x.Label, "Código"));

        Assert.Null(cut.Find("input").GetAttribute("data-ns-arrival-focus"));
    }
}
