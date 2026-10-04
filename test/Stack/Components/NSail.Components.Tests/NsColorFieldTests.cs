// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>The dropdown was the only way in (nsail#466): <c>MudColorPicker</c> defaults its
/// text box to read-only, so a person who already knows the hex still had to open the popover.
/// <c>Editable="true"</c> lifts that, and <c>ShowAlpha="false"</c> keeps the box's canonical
/// hex matching the plain 6-digit strings every caller already stores (BrandingColors,
/// CreateConsultingRoomPage) — an alpha-suffixed round trip would otherwise rewrite the model
/// the instant the field first renders (caught by FieldArrivalProbeTests' dirty-on-arrival
/// case). The vendor refuses unparseable text itself, below TextChanged: measured directly
/// (SetInputStringAsync never invokes TextChanged for text MudColor.TryParse rejects), not
/// assumed — so no guard is layered on top of it here.</summary>
public sealed class NsColorFieldTests : BunitContext, IAsyncLifetime
{
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    void Compose()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task TypingAHexIntoTheBox_SetsTheColor()
    {
        Compose();

        string? captured = null;
        var cut = Render<NsColorField>(ps => ps
            .Add(p => p.Value, "#1976d2")
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<string?>(this, v => captured = v)));

        await cut.InvokeAsync(() => cut.Find("input").Change("#3366FF"));

        Assert.Equal("#3366ff", captured);
        Assert.Equal("#3366ff", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public async Task TypingTextThatIsNotAColor_LeavesTheLastGoodValueStanding()
    {
        Compose();

        var changes = new List<string?>();
        var cut = Render<NsColorField>(ps => ps
            .Add(p => p.Value, "#1976d2")
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<string?>(this, v => changes.Add(v))));

        await cut.InvokeAsync(() => cut.Find("input").Change("not a color"));

        Assert.Empty(changes);
        Assert.Equal("#1976d2", cut.Instance.Value);
    }

    static RouteTable BuildRouteTable()
    {
        return new(typeof(NsColorFieldTests).Assembly, Array.Empty<System.Reflection.Assembly>());
    }

    /// <summary>The same round trip as the two tests above, now inside a real NsForm — the
    /// EditContext's own validation-state notification re-renders the field the instant
    /// ValueChanged lands (NsFieldBase.HandleValidationStateChanged), the same race that once
    /// wiped NsDateField's in-progress text. Proves the picker's Value binding survives it:
    /// the box still shows the color the user typed, not something an intermediate render
    /// clobbered.</summary>
    [Fact]
    public async Task TypingAHexInsideAForm_SurvivesTheEditContextsOwnRerender()
    {
        Compose();
        Services.AddComponentServices();

        var host = Render<FieldProbeHost>(p => p
            .Add(x => x.RouteTable, BuildRouteTable())
            .Add(x => x.Kind, "color")
            .Add(x => x.Mode, "preset"));

        await host.InvokeAsync(() => host.Find("input").Change("#3366FF"));

        Assert.Equal("#3366ff", host.Find("input").GetAttribute("value"));
    }
}
