// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>The multi-valued field wears NsSelect's own rule: its displayed text is RESOLVED
/// out of Items, so it floats its label on the SELECTION rather than on the text it managed to
/// name (nsail#1218). No caller loads its Items from a read today — every one of them offers a
/// fixed vocabulary — and the declaration is here so the first that does cannot inherit the
/// defect its single-valued twin was fixed for.</summary>
public sealed class NsMultiSelectTests : BunitContext, IAsyncLifetime
{
    public NsMultiSelectTests()
    {
        Services.AddMudServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // MudPopoverProvider resolves a MudBlazor service that is IAsyncDisposable-only, which
    // bUnit's synchronous teardown cannot dispose (NsSelectTests' own note).
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

    [Fact]
    public void NsMultiSelect_FloatsItsLabelForASelectionItsItemsCannotNameYet()
    {
        var cut = Render<MultiSelectValueHost<SelectRef, Guid>>(ps => ps
            .Add(p => p.Items, Array.Empty<SelectRef>())
            .Add(p => p.ItemValue, item => item.Id)
            .Add(p => p.ItemText, item => item.DisplayName)
            .Add(p => p.Label, "Recursos")
            .Add(p => p.Value, new[] { Beta.Id }));

        Assert.Equal(string.Empty, cut.Find("input.mud-select-input").GetAttribute("value"));
        Assert.Contains("mud-shrink", cut.Find("div.mud-input").ClassList);
    }

    [Fact]
    public void NsMultiSelect_RestsItsLabelWhenNothingIsSelected()
    {
        var cut = Render<MultiSelectValueHost<SelectRef, Guid>>(ps => ps
            .Add(p => p.Items, new[] { Alpha, Beta })
            .Add(p => p.ItemValue, item => item.Id)
            .Add(p => p.ItemText, item => item.DisplayName)
            .Add(p => p.Label, "Recursos")
            .Add(p => p.Value, Array.Empty<Guid>()));

        Assert.DoesNotContain("mud-shrink", cut.Find("div.mud-input").ClassList);
    }
}
