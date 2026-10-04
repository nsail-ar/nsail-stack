// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>NsHelp is the circled ? that carries the paragraph a form should not (nsail#182).
/// What is pinned here is the whole contract: the glyph button names itself with the localized
/// "Help" so a control whose only content is an icon is not anonymous; the paragraph does not
/// exist in the document until it is asked for — the point of the component, and what a
/// display:none popover would not have delivered; and both dismissals answer.
///
/// What bUnit cannot reach, said plainly: it renders no CSS and runs no MudBlazor JS, so the
/// bubble's placement beside its anchor and the click-catcher's transparency are not pinned
/// here — only that the catcher is rendered and that clicking it closes. Escape is pinned as
/// the keydown reaching the component's own wrapper, which is where the focused trigger's
/// key event bubbles to in a browser.</summary>
public sealed class NsHelpTests : BunitContext, IAsyncLifetime
{
    const string Paragraph =
        "El host donde responde esta sucursal, para que su marca pinte el inicio de sesión.";

    public NsHelpTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            ["Common.Help"] = "Ayuda",
            ["Directory.Organization.DomainHelper"] = Paragraph
        })]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // MudPopoverProvider resolves a MudBlazor service that is IAsyncDisposable-only and
    // internal, so bUnit's synchronous teardown cannot dispose it (NsPopoverPaperTests' own
    // note, same fixture family).
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    IRenderedComponent<HelpHost> Render()
    {
        return Render<HelpHost>(p =>
            p.Add(x => x.Key, "Directory.Organization.DomainHelper"));
    }

    [Fact]
    public void AtRest_IsAGlyphButtonNamedByTheLocalizedHelp()
    {
        var cut = Render();

        var button = cut.Find(".ns-help button");

        Assert.Equal("Ayuda", button.GetAttribute("aria-label"));
        Assert.NotEmpty(button.QuerySelectorAll("svg"));

        // The button's own accessible name is the only text it carries: the glyph says what it
        // is, the paragraph says nothing until asked.
        Assert.Equal(string.Empty, button.TextContent.Trim());

        // The flat rung's bare glyph, and no box: the circled ? is not one of the squares a
        // filled intention takes when it has no word to draw (nsail#925).
        Assert.Contains("mud-icon-button", button.ClassList);
        Assert.DoesNotContain("ns-square", button.ClassList);
    }

    /// <summary>The rent the component exists to stop paying: at rest the paragraph is not in
    /// the document at all — not merely hidden by a stylesheet the server never sends.</summary>
    [Fact]
    public void AtRest_TheParagraphIsNotInTheDocument()
    {
        var cut = Render();

        Assert.DoesNotContain(Paragraph, cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AClick_RendersTheResolvedText()
    {
        var cut = Render();

        await cut.InvokeAsync(() => cut.Find(".ns-help button").Click());

        Assert.Contains(Paragraph, cut.Find(".ns-help-text").TextContent, StringComparison.Ordinal);

        // Resolved, never echoed: the key itself never reaches the screen when the catalog
        // answers for it.
        Assert.DoesNotContain("DomainHelper", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>A key nobody translated renders as itself — untranslated strings are visible
    /// by doctrine, never silently humanized (NsTabLabelTests holds the same line).</summary>
    [Fact]
    public async Task AnUntranslatedKey_RendersAsItself()
    {
        var cut = Render<HelpHost>(p => p.Add(x => x.Key, "Directory.Nothing.Helper"));

        await cut.InvokeAsync(() => cut.Find(".ns-help button").Click());

        Assert.Equal("Directory.Nothing.Helper", cut.Find(".ns-help-text").TextContent.Trim());
    }

    [Fact]
    public async Task Escape_DismissesTheHelp()
    {
        var cut = Render();

        await cut.InvokeAsync(() => cut.Find(".ns-help button").Click());
        Assert.Contains(Paragraph, cut.Markup, StringComparison.Ordinal);

        await cut.InvokeAsync(() => cut.Find(".ns-help").KeyDown(new KeyboardEventArgs { Key = "Escape" }));

        Assert.DoesNotContain(Paragraph, cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>A key that is not Escape leaves it open — the dismissal is a decision, not
    /// anything the keyboard happens to say while the bubble is up.</summary>
    [Fact]
    public async Task AnotherKey_LeavesTheHelpOpen()
    {
        var cut = Render();

        await cut.InvokeAsync(() => cut.Find(".ns-help button").Click());
        await cut.InvokeAsync(() => cut.Find(".ns-help").KeyDown(new KeyboardEventArgs { Key = "a" }));

        Assert.Contains(Paragraph, cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnOutsideClick_DismissesTheHelp()
    {
        var cut = Render();

        await cut.InvokeAsync(() => cut.Find(".ns-help button").Click());

        // The catcher the vendor paints over the page while the bubble is up: an outside click
        // lands on it, never on whatever it covers.
        await cut.InvokeAsync(() => cut.Find(".mud-overlay").Click());

        Assert.DoesNotContain(Paragraph, cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASecondClickOnTheGlyph_DismissesTheHelp()
    {
        var cut = Render();

        await cut.InvokeAsync(() => cut.Find(".ns-help button").Click());
        await cut.InvokeAsync(() => cut.Find(".ns-help button").Click());

        Assert.DoesNotContain(Paragraph, cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>A disclosure trigger owes the reader the state of what it discloses: the glyph
    /// says what the button is, aria-expanded says whether the paragraph it opens is already on
    /// screen. Closed is announced, not omitted — a trigger silent until open reads as an
    /// ordinary button for the whole time it is closed, which is most of the time.</summary>
    [Fact]
    public async Task TheTrigger_AnnouncesWhetherTheBubbleIsOpen()
    {
        var cut = Render();

        Assert.Equal("false", cut.Find(".ns-help button").GetAttribute("aria-expanded"));

        await cut.InvokeAsync(() => cut.Find(".ns-help button").Click());

        Assert.Equal("true", cut.Find(".ns-help button").GetAttribute("aria-expanded"));
    }

    /// <summary>The API promise: one parameter, the key. Anything a page could use to name the
    /// bubble's mechanics — a placement, a width, a vendor surface — has to break this first.</summary>
    [Fact]
    public void NsHelp_ExposesOnlyKey()
    {
        var parameters = typeof(NsHelp)
            .GetProperties()
            .Where(p => p.GetCustomAttributes(typeof(Microsoft.AspNetCore.Components.ParameterAttribute), true).Length != 0)
            .Select(p => p.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["Key"], parameters);
    }

    /// <summary>No vendor type in a public NSail API — the whole point of the popover living
    /// behind this component.</summary>
    [Fact]
    public void NsHelp_NamesNoVendorTypeInItsApi()
    {
        var vendor = typeof(NsHelp)
            .GetProperties()
            .Where(p => p.GetCustomAttributes(typeof(Microsoft.AspNetCore.Components.ParameterAttribute), true).Length != 0)
            .Where(p => p.PropertyType.Namespace?.StartsWith("MudBlazor", StringComparison.Ordinal) == true)
            .ToArray();

        Assert.Empty(vendor);
    }
}
