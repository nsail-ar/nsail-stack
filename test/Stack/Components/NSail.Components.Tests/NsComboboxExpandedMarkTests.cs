// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#1470: Enter over an open list picks the highlighted option, and the form the box
/// stands in is not part of that gesture — the browser's implicit submission is prevented on the
/// keydown for exactly the keystrokes the list will answer. The decision is made from the DOM, per
/// keystroke, because whether a list is open is a property of the moment and not of the last
/// render (ns.js, onDocumentKeyDown); what it reads is the box's own combobox state.
/// <para>This is that read's other half: the markup the guard keys on, pinned here so the day a
/// vendor upgrade stops writing it the failure has a name instead of being a sale written by a
/// keystroke nobody was watching. Every case asserts on the INPUT, because the guard scopes itself
/// to the element the keystroke lands on — a button that carries the same attribute (an expander's
/// chevron, a menu's face) keeps its own activation.</para>
/// <para>The guard itself cannot be proved at this layer — bUnit renders through AngleSharp, which
/// performs no implicit form submission — and is held by LookupEnterPickTests in the browser.</para>
/// </summary>
public sealed class NsComboboxExpandedMarkTests : BunitContext, IAsyncLifetime
{
    public NsComboboxExpandedMarkTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsComboboxExpandedMarkTests).Assembly, []));
        Services.AddScoped<RootSurface>();
        Services.AddScoped<SurfaceHistory>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // MudPopoverProvider resolves a MudBlazor service that is IAsyncDisposable-only, so bUnit's
    // synchronous teardown cannot dispose it (NsLookupCreateEntryTests' own note).
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

    IRenderedComponent<SearchLookupHost> MountLookup()
    {
        return Render<SearchLookupHost>(p => p
            .Add(x => x.Rows, Chart)
            .Add(x => x.Lazy, false));
    }

    static string? Mark(IRenderedComponent<IComponent> cut, string attribute)
    {
        return cut.Find("input").GetAttribute(attribute);
    }

    static void Reads(IRenderedComponent<IComponent> cut, string expanded)
    {
        cut.WaitForAssertion(
            () => Assert.Equal(expanded, Mark(cut, "aria-expanded")),
            TimeSpan.FromSeconds(5));
    }

    /// <summary>The box says what it is before anything is open: a combobox, and the guard reads
    /// that word to tell a lookup from every other input a form holds — a plain text field in a
    /// one-field form still submits on Enter.</summary>
    [Fact]
    public void AClosedLookupIsACollapsedCombobox()
    {
        var cut = MountLookup();

        Assert.Equal("combobox", Mark(cut, "role"));
        Assert.Equal("false", Mark(cut, "aria-expanded"));
    }

    [Fact]
    public async Task AnOpenListMarksTheBoxExpanded()
    {
        var cut = MountLookup();

        await cut.Find("input").FocusAsync(new FocusEventArgs());

        Reads(cut, "true");
    }

    /// <summary>A term nothing matched leaves the create entry standing and no option to pick, so
    /// the box reads collapsed and Enter stays the form's — the story's own "empty keeps today's
    /// behaviour", answered by the same word the guard already reads.</summary>
    [Fact]
    public async Task AListWithNoOptionsLeavesTheBoxCollapsed()
    {
        var cut = MountLookup();

        await cut.Find("input").FocusAsync(new FocusEventArgs());

        Reads(cut, "true");

        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "Nada" });

        Reads(cut, "false");
    }

    /// <summary>fields.md says the same rule covers the selects, and this is what that sentence
    /// rests on: their closed box is the same combobox and their open one carries the same
    /// expanded mark, on the same element — the readonly input the keystroke lands on. So the
    /// guard needs no second condition to cover them, and the day the vendor moves the mark to
    /// the wrapper the doc's claim fails here by name rather than in a form somebody saved by
    /// choosing from a list.</summary>
    [Fact]
    public async Task ASelectPublishesTheSameMark()
    {
        var cut = Render<SelectValueHost<SelectRef, Guid>>(p => p
            .Add(x => x.Items, Chart)
            .Add(x => x.ItemValue, item => item.Id)
            .Add(x => x.ItemText, item => item.DisplayName)
            .Add(x => x.Label, "Cuenta")
            .Add(x => x.Value, Guid.Empty));

        Assert.Equal("combobox", Mark(cut, "role"));
        Assert.Equal("false", Mark(cut, "aria-expanded"));

        await cut.Find("div.mud-input-control").MouseDownAsync(new MouseEventArgs());

        Reads(cut, "true");
    }

    [Fact]
    public async Task AMultiSelectPublishesTheSameMark()
    {
        var cut = Render<MultiSelectValueHost<SelectRef, Guid>>(p => p
            .Add(x => x.Items, Chart)
            .Add(x => x.ItemValue, item => item.Id)
            .Add(x => x.ItemText, item => item.DisplayName)
            .Add(x => x.Label, "Cuentas")
            .Add(x => x.Value, []));

        Assert.Equal("combobox", Mark(cut, "role"));
        Assert.Equal("false", Mark(cut, "aria-expanded"));

        await cut.Find("div.mud-input-control").MouseDownAsync(new MouseEventArgs());

        Reads(cut, "true");
    }
}
