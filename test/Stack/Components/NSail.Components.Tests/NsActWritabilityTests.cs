// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#1321: the host read nsail#1314 put on NsCollectionBase reaches every way in
/// the Stack's own chrome draws and nothing a kit hand-draws beside it — three collection
/// editors draw their Add, their Edit and their Delete as NsActions of their own, outside any
/// host, and those stayed clickable inside a form that refuses writes.
///
/// What is held here is the act's own answer: an act drawn under an NsForm is a way IN unless
/// it declares it changes nothing the form holds (ActionItem.Writes), so the default withholds
/// it and no kit names the cascade. Both of an ActionItem's mouths answer — the bare NsAction
/// and the toolbar an outlet hands its verbs to — and the toolbar drops what it withholds
/// BEFORE it counts, or the cap spends a slot on nothing and the kebab opens on an empty
/// list.</summary>
public sealed class NsActWritabilityTests : BunitContext, IAsyncLifetime
{
    public NsActWritabilityTests()
    {
        Services.AddMudServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            ["Actions.Rename"] = "Renombrar",
            ["Actions.Probe"] = "Probar",
            ["Actions.Archive"] = "Archivar",
            ["Actions.Contact"] = "Contactar",
            ["Actions.ProbeOtherPage"] = "Ficha",
        })]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsActWritabilityTests).Assembly, []));
        Services.AddSingleton<DialogManager>(new CountingDialogManager());
        Services.AddSingleton<Mediator>(new CapturingMediator());
        Services.AddScoped<RootSurface>();
        Services.AddScoped<SurfaceHistory>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // MudPopoverProvider resolves a MudBlazor service that is IAsyncDisposable-only, so bUnit's
    // synchronous teardown cannot dispose it (NsCollectionWritabilityTests' own note).
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    static IReadOnlyList<string> Acts(IRenderedComponent<ActFormHost> cut)
    {
        return cut
            .FindAll("button[aria-label], a[aria-label]")
            .Select(control => control.GetAttribute("aria-label") ?? "")
            .ToList();
    }

    static void AssertNoWayIn(IRenderedComponent<ActFormHost> cut)
    {
        var acts = Acts(cut);

        Assert.DoesNotContain("Renombrar", acts);
        Assert.DoesNotContain("Archivar", acts);

        // What changes nothing the form holds stays: the probe beside the fields, the verb the
        // toolbar was handed, and the link that only goes somewhere.
        Assert.Contains("Probar", acts);
        Assert.Contains("Contactar", acts);
        Assert.Contains("Ficha", acts);

        // The toolbar counts what survives, so the cap that held two verbs holds one and the
        // kebab that folded the remainder has no remainder left to open on.
        Assert.Empty(cut.Find(".ns-action-toolbar").QuerySelectorAll(".mud-menu"));

        // The cell's reserve is geometry and not a count of what rendered: the icons beside it
        // must not move because a form went read-only (ns-mud.css, --ns-action-slots).
        Assert.Contains("--ns-action-slots: 3", cut.Find(".ns-action-toolbar").GetAttribute("style") ?? "");
    }

    [Fact]
    public void AWritableFormDrawsEveryActItWasGiven()
    {
        var cut = Render<ActFormHost>();

        var acts = Acts(cut);

        Assert.Contains("Renombrar", acts);
        Assert.Contains("Probar", acts);
        Assert.Contains("Ficha", acts);

        // Two verbs against a cap of one: the way in takes the slot and the other folds into
        // the kebab, which is the arithmetic the read-only case has to come out of differently.
        Assert.Contains("Archivar", acts);
        Assert.NotEmpty(cut.Find(".ns-action-toolbar").QuerySelectorAll(".mud-menu"));
    }

    [Fact]
    public void AReadOnlyFormWithholdsTheActsThatWrite()
    {
        var cut = Render<ActFormHost>(p => p.Add(x => x.ReadOnly, true));

        AssertNoWayIn(cut);
    }

    [Fact]
    public void ADisabledFormWithholdsTheActsThatWrite()
    {
        var cut = Render<ActFormHost>(p => p.Add(x => x.Disabled, true));

        AssertNoWayIn(cut);
    }

    /// <summary>A form is disabled for the length of every submit, so the act does not wait for
    /// a screen that mounts ReadOnly to be taken away and put back — it has to answer a cascade
    /// that moves under a mounted component.</summary>
    [Fact]
    public void AFormThatTurnsReadOnlyTakesTheWayInAndGivesItBack()
    {
        var cut = Render<ActFormHost>();

        cut.Render(p => p.Add(x => x.ReadOnly, true));

        AssertNoWayIn(cut);

        cut.Render(p => p.Add(x => x.ReadOnly, false));

        Assert.Contains("Renombrar", Acts(cut));
        Assert.Contains("Archivar", Acts(cut));
    }
}
