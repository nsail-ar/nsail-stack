// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>The closed field's text is derived from Items, never from the vendor's converter.
/// That is what keeps a bound value no item carries — an unchosen required Guid, the state
/// every create form starts in — from being shown to a human as "00000000-…", and it is the
/// shape NsMultiSelect and NsAutocomplete already had.</summary>
public sealed class NsSelectTests : BunitContext, IAsyncLifetime
{
    public NsSelectTests()
    {
        Services.AddMudServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // MudPopoverProvider, which the closed field needs to render, resolves a MudBlazor service
    // that only implements IAsyncDisposable and is internal to that assembly, so it cannot be
    // replaced from here the way IKeyInterceptorService is. Tearing down through xunit's
    // IAsyncLifetime makes it call BunitContext's async DisposeAsync instead of the
    // synchronous Dispose that fails on it.
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
    public void NsSelect_ShowsNothingForAValueNoItemCarries()
    {
        var cut = Render<SelectValueHost<SelectRef, Guid>>(ps => ps
            .Add(p => p.Items, new[] { Alpha, Beta })
            .Add(p => p.ItemValue, item => item.Id)
            .Add(p => p.ItemText, item => item.DisplayName)
            .Add(p => p.Label, "Recurso")
            .Add(p => p.Placeholder, "Elegí un recurso")
            .Add(p => p.Required, true)
            .Add(p => p.Value, Guid.Empty));

        var input = cut.Find("input.mud-select-input");

        Assert.Equal(string.Empty, input.GetAttribute("value"));
        Assert.Equal("Elegí un recurso", input.GetAttribute("placeholder"));
        Assert.DoesNotContain("00000000-0000-0000-0000-000000000000", cut.Markup);
    }

    [Fact]
    public void NsSelect_ShowsTheItemsTextForAValueItCarries()
    {
        var cut = Render<SelectValueHost<SelectRef, Guid>>(ps => ps
            .Add(p => p.Items, new[] { Alpha, Beta })
            .Add(p => p.ItemValue, item => item.Id)
            .Add(p => p.ItemText, item => item.DisplayName)
            .Add(p => p.Label, "Recurso")
            .Add(p => p.Value, Beta.Id));

        Assert.Equal("Beta", cut.Find("input.mud-select-input").GetAttribute("value"));
    }

    /// <summary>nsail#1789: the open list and the closed field are allowed to say different
    /// things. ItemTemplate draws the OPTION — a balance beside the value, a figure the operator
    /// chooses on — while ItemText still names the value, so the field does not go on saying a
    /// count from whenever it was picked (ui/fields.md, Lookups).</summary>
    [Fact]
    public async Task NsSelect_DrawsTheTemplateInTheListAndTheItemsTextInTheField()
    {
        var cut = Render<SelectValueHost<SelectRef, Guid>>(ps => ps
            .Add(p => p.Items, new[] { Alpha, Beta })
            .Add(p => p.ItemValue, item => item.Id)
            .Add(p => p.ItemText, item => item.DisplayName)
            .Add(p => p.ItemTemplate, item => builder => builder.AddContent(0, $"{item.DisplayName} — 5 en stock"))
            .Add(p => p.Label, "Recurso")
            .Add(p => p.Value, Beta.Id));

        Assert.Equal("Beta", cut.Find("input.mud-select-input").GetAttribute("value"));

        Assert.Equal(["Alpha — 5 en stock", "Beta — 5 en stock"], await Offered(cut));
    }

    /// <summary>And a select that declares no template is unchanged: the option reads as the
    /// item's own text, which is every select in the tree.</summary>
    [Fact]
    public async Task NsSelect_DrawsTheItemsTextInTheListWhereNoTemplateIsDeclared()
    {
        var cut = Render<SelectValueHost<SelectRef, Guid>>(ps => ps
            .Add(p => p.Items, new[] { Alpha, Beta })
            .Add(p => p.ItemValue, item => item.Id)
            .Add(p => p.ItemText, item => item.DisplayName)
            .Add(p => p.Label, "Recurso")
            .Add(p => p.Value, Beta.Id));

        Assert.Equal(["Alpha", "Beta"], await Offered(cut));
    }

    // A select opens on mousedown — the vendor's own control, read-only text input included —
    // and its options only exist in the tree while it is open.
    static async Task<List<string>> Offered(IRenderedComponent<SelectValueHost<SelectRef, Guid>> cut)
    {
        await cut.InvokeAsync(() => cut.Find(".mud-input-control").MouseDownAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs()));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-list-item")), TimeSpan.FromSeconds(5));

        return [.. cut.FindAll(".mud-list-item").Select(item => item.TextContent.Trim())];
    }

    /// <summary>nsail#1218: the wrapper fills Items from a read, so the field holds its value
    /// for the render before it can name it. The label floats on the VALUE — it is already up
    /// when the text lands, instead of sitting where the text is about to be painted.</summary>
    [Fact]
    public void NsSelect_FloatsItsLabelForAValueItsItemsCannotNameYet()
    {
        var cut = Render<SelectValueHost<SelectRef, Guid>>(ps => ps
            .Add(p => p.Items, Array.Empty<SelectRef>())
            .Add(p => p.ItemValue, item => item.Id)
            .Add(p => p.ItemText, item => item.DisplayName)
            .Add(p => p.Label, "Recurso")
            .Add(p => p.Value, Beta.Id));

        Assert.Equal(string.Empty, cut.Find("input.mud-select-input").GetAttribute("value"));
        Assert.Contains("mud-shrink", cut.Find("div.mud-input").ClassList);

        cut.Render(ps => ps.Add(p => p.Items, new[] { Alpha, Beta }));

        Assert.Equal("Beta", cut.Find("input.mud-select-input").GetAttribute("value"));
        Assert.Contains("mud-shrink", cut.Find("div.mud-input").ClassList);
    }

    /// <summary>The other half of the same rule: a field nobody answered still rests its label
    /// inside the empty box, so the value arm above cannot float every label on the screen.
    /// Guid.Empty is exactly the state a required create form starts in.</summary>
    [Fact]
    public void NsSelect_RestsItsLabelOnABoxNobodyAnswered()
    {
        var cut = Render<SelectValueHost<SelectRef, Guid>>(ps => ps
            .Add(p => p.Items, new[] { Alpha, Beta })
            .Add(p => p.ItemValue, item => item.Id)
            .Add(p => p.ItemText, item => item.DisplayName)
            .Add(p => p.Label, "Recurso")
            .Add(p => p.Value, Guid.Empty));

        Assert.DoesNotContain("mud-shrink", cut.Find("div.mud-input").ClassList);
    }

    /// <summary>The optional select names its own empty state with an option carrying the null
    /// value ("No place"), and that keeps working: the text comes from the item that matches,
    /// so a value the list can name is still named.</summary>
    [Fact]
    public void NsSelect_ShowsTheOptionThatCarriesTheEmptyValue()
    {
        var items = new List<Option<Guid?>>
        {
            new(null, "Sin lugar"),
            new(Alpha.Id, "Consultorio 1"),
        };

        var cut = Render<SelectValueHost<Option<Guid?>, Guid?>>(ps => ps
            .Add(p => p.Items, items)
            .Add(p => p.Label, "Lugar")
            .Add(p => p.Value, null));

        Assert.Equal("Sin lugar", cut.Find("input.mud-select-input").GetAttribute("value"));
    }

    /// <summary>And its label floats over it. A null the list NAMES is an answer like any
    /// other, so the box is not empty — the state Nuevo Turno opens in when no slot derived a
    /// place. It floats on the text, which is the arm the value arm above does not replace.</summary>
    [Fact]
    public void NsSelect_FloatsItsLabelOverTheOptionThatCarriesTheEmptyValue()
    {
        var items = new List<Option<Guid?>>
        {
            new(null, "Sin lugar"),
            new(Alpha.Id, "Consultorio 1"),
        };

        var cut = Render<SelectValueHost<Option<Guid?>, Guid?>>(ps => ps
            .Add(p => p.Items, items)
            .Add(p => p.Label, "Lugar")
            .Add(p => p.Value, null));

        Assert.Contains("mud-shrink", cut.Find("div.mud-input").ClassList);
    }
}
