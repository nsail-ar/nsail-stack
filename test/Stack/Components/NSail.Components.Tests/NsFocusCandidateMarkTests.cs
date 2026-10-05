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

/// <summary>nsail#2000: a create screen whose first field is a select handed the cursor to the
/// field AFTER it. `focusFirst` (ns.js) was skipping every `[readonly]` candidate to skip a field
/// the screen locked, and a select's own input carries that attribute unconditionally — the vendor
/// paints the chosen value into a text box nobody types into.
/// <para>So the skip reads <c>aria-readonly</c>, which <c>NsFieldBase</c> writes for a field that
/// is read-only and nothing writes otherwise, and the swallow that keeps a lookup's popover shut
/// is armed off <c>aria-autocomplete</c> rather than <c>role="combobox"</c> — a select publishes
/// the role too (NsComboboxExpandedMarkTests) and opens on a mousedown, not on a focus.</para>
/// <para>This is the markup half of that query, pinned here because there is no JS harness in the
/// tree: the day a vendor upgrade moves either word, the failure has a name instead of being a
/// cursor landing one field down. The query itself runs in a browser and is held by
/// CreateFocusWalkTests.</para></summary>
public sealed class NsFocusCandidateMarkTests : BunitContext, IAsyncLifetime
{
    public NsFocusCandidateMarkTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsFocusCandidateMarkTests).Assembly, []));
        Services.AddScoped<RootSurface>();
        Services.AddScoped<SurfaceHistory>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // MudPopoverProvider resolves a MudBlazor service that is IAsyncDisposable-only, so bUnit's
    // synchronous teardown cannot dispose it (NsComboboxExpandedMarkTests' own note). One host per
    // test for the same reason the provider allows only one: a second render in the same context
    // claims the overlay section twice.
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    static readonly SelectRef[] Chart =
    [
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Caja en pesos"),
        new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Banco Nación"),
    ];

    IRenderedComponent<SelectValueHost<SelectRef, Guid>> MountSelect(bool locked)
    {
        return Render<SelectValueHost<SelectRef, Guid>>(p => p
            .Add(x => x.Items, Chart)
            .Add(x => x.ItemValue, item => item.Id)
            .Add(x => x.ItemText, item => item.DisplayName)
            .Add(x => x.Label, "Cuenta")
            .Add(x => x.ReadOnly, locked)
            .Add(x => x.Value, Guid.Empty));
    }

    IRenderedComponent<SearchLookupHost> MountLookup(bool locked)
    {
        return Render<SearchLookupHost>(p => p
            .Add(x => x.Rows, Chart)
            .Add(x => x.Lazy, false)
            .Add(x => x.ReadOnly, locked));
    }

    static void Locked(IRenderedComponent<IComponent> cut, bool expected)
    {
        Assert.Equal(expected ? "true" : null, cut.Find("input").GetAttribute("aria-readonly"));
    }

    /// <summary>The defect itself, as markup: an open select's box IS readonly to the browser and
    /// says nothing about being locked — so a query reading the attribute walks past Ejercicio,
    /// Plantilla, Tipo and Origen, and a query reading the mark lands on them.</summary>
    [Fact]
    public void AnOpenSelectIsReadonlyToTheBrowserAndNotMarkedLocked()
    {
        var cut = MountSelect(locked: false);

        Assert.True(cut.Find("input").HasAttribute("readonly"));

        Locked(cut, false);
    }

    [Fact]
    public void ALockedSelectIsMarkedLocked()
    {
        Locked(MountSelect(locked: true), true);
    }

    /// <summary>A lookup's box is typed into, so the vendor leaves it writable — which is why the
    /// attribute could never have told the two apart in the first place.</summary>
    [Fact]
    public void AnOpenLookupIsNeitherReadonlyNorMarkedLocked()
    {
        var cut = MountLookup(locked: false);

        Assert.False(cut.Find("input").HasAttribute("readonly"));

        Locked(cut, false);
    }

    [Fact]
    public void ALockedLookupIsMarkedLocked()
    {
        Locked(MountLookup(locked: true), true);
    }

    /// <summary>What arms the popover swallow. The select is chosen from — "none" — and the lookup
    /// searches as it is typed into, which is the same thing OpenOnFocus reacts to.</summary>
    [Fact]
    public void ASelectOffersNoListAsItIsTypedInto()
    {
        Assert.Equal("none", MountSelect(locked: false).Find("input").GetAttribute("aria-autocomplete"));
    }

    [Fact]
    public void ALookupOffersItsListAsItIsTypedInto()
    {
        Assert.Equal("list", MountLookup(locked: false).Find("input").GetAttribute("aria-autocomplete"));
    }
}
