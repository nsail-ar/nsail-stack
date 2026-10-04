// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>The door a picker draws when its set is empty (nsail#853): a screen that cannot
/// proceed for a missing satellite says WHICH one and links to where it is made, instead of
/// leaving an empty dropdown or answering a raw not-found. Both sentences are derived — the
/// concept from the item type's own key, the act from the same Common.CreateNew template the
/// dropdown's create entry composes — so no field, no screen and no kit writes either, and no
/// caller writes an address.</summary>
public sealed class NsMissingTests : BunitContext
{
    const string Concept = "Components.MissingProbe";

    public NsMissingTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddComponentServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsMissingTests).Assembly, []));
        Services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
    }

    /// <summary>The whole contract in one render: the fact names the concept, the act names it
    /// too, and the address is the one the caller handed over — resolved at the anchor.</summary>
    [Fact]
    public void TheDoorNamesTheConceptAndLinksToWhereItIsMade()
    {
        Translated();

        var cut = Render<NsMissing<MissingProbe>>(p => p.Add(x => x.CreateRoute, "products/stores/new"));

        var door = Assert.Single(cut.FindAll(".ns-field-missing"));

        Assert.Contains("no hay Depósito", door.TextContent, StringComparison.Ordinal);
        Assert.Contains("Crear Depósito", door.TextContent, StringComparison.Ordinal);
        Assert.Contains("products/stores/new", cut.Find("a").GetAttribute("href"), StringComparison.Ordinal);
    }

    /// <summary>The door composes the same act the dropdown's create entry composes, so it has
    /// to stack the same way: the window it opens exists to hand a value back to the field
    /// standing underneath (StoreSelect subscribes to StoreSaved and calls ValueChanged), and an
    /// entry of its own would leave that finished window one Back away from the form it just
    /// fed. Marked on this component's own link — no call site passes it, and the empty picker
    /// is the only thing that draws this door.</summary>
    [Fact]
    public async Task TheDoorOpensItsSurfaceWithNoHistoryEntryOfItsOwn()
    {
        Translated();

        var navigation = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("/optical/work-orders/new");

        var cut = Render<NsMissing<MissingProbe>>(p => p
            .Add(x => x.CreateRoute, "products/stores/new")
            .Add(x => x.Target, Surfaces.Aside));

        await cut.InvokeAsync(() => cut.Find(".ns-field-missing a").Click());

        // bUnit's History is newest first, and a replacing write drops the entry it replaced.
        var write = navigation.History.First();

        Assert.Contains("aside=products%2Fstores%2Fnew", write.Uri, StringComparison.Ordinal);
        Assert.True(write.Options.ReplaceHistoryEntry);
    }

    /// <summary>A type whose concept nobody translated renders nothing at all, rather than a
    /// sentence with a hole where the name goes: the door is a courtesy, and a courtesy nobody
    /// can read is noise.</summary>
    [Fact]
    public void AnUntranslatedConceptDrawsNothing()
    {
        Services.AddSingleton(new StringCatalog(
        [
            new FixedStrings(new Dictionary<string, string>
            {
                ["Common.NoOptions"] = "Todavía no hay {0} para elegir.",
                ["Common.CreateNew"] = "Crear {0}...",
            }),
        ]));

        var cut = Render<NsMissing<MissingProbe>>(p => p.Add(x => x.CreateRoute, "products/stores/new"));

        Assert.Empty(cut.FindAll(".ns-field-missing"));
    }

    void Translated()
    {
        Services.AddSingleton(new StringCatalog(
        [
            new FixedStrings(new Dictionary<string, string>
            {
                ["Common.NoOptions"] = "Todavía no hay {0} para elegir.",
                ["Common.CreateNew"] = "Crear {0}...",
                [Concept] = "Depósito",
            }),
        ]));
    }
}

/// <summary>Stands in for a kit's own Ref model: NSail.Components.MissingProbe resolves to the
/// key the door reads, so the derivation under test is the real one.</summary>
public sealed class MissingProbe;
