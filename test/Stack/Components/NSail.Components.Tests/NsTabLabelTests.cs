// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>Rule three: a tab header carries label text and nothing else, and that text comes
/// through the localization machinery (StringCatalog to LanguageProvider to StringManager)
/// rather than being written into the markup. A translated key renders its text; a key
/// nobody translated renders as itself, so an untranslated tab is visible instead of
/// silently humanized.</summary>
public sealed class NsTabLabelTests : BunitContext
{
    public NsTabLabelTests()
    {
        Services.AddMudServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, Fixtures.NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            ["Directory.Party.Identity"] = "Identidad"
        })]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void TabLabels_ResolveThroughTheLocalizationMachinery()
    {
        var cut = Render<TabTitleHost>();

        var tabHeaders = cut.FindAll(".mud-tabs-tabbar .mud-tab");
        Assert.Equal(2, tabHeaders.Count);

        // The key is in the catalog: the header shows the text, never the key.
        Assert.Equal("Identidad", tabHeaders[0].TextContent.Trim());
        Assert.DoesNotContain("Directory.Party.Identity", cut.Markup);

        // The key is in no dictionary: it renders as itself. Untranslated strings are
        // visible by doctrine — the alternative is a tab silently labelled "Untranslated".
        Assert.Equal("Directory.Party.Untranslated", tabHeaders[1].TextContent.Trim());
    }

    /// <summary>Chrome only. The header is label text plus the problem mark and nothing
    /// else: no icon slot, no per-tab toolbar. Pinned structurally so growing NsTab on
    /// speculation has to break a test first.</summary>
    [Fact]
    public void TabHeader_CarriesTextAndNoOtherChrome()
    {
        var cut = Render<TabTitleHost>();

        var header = cut.FindAll(".mud-tabs-tabbar .mud-tab")[0];

        Assert.Empty(header.QuerySelectorAll("svg"));
        Assert.Empty(header.QuerySelectorAll("button"));
        Assert.Empty(header.QuerySelectorAll(".mud-icon-root"));

        // No badge either, until a field inside the tab reports a problem.
        Assert.DoesNotContain("mud-badge", header.OuterHtml);
    }

    /// <summary>The parameter surface is the API promise: the label, the content, and the tab's
    /// invariant Name — nothing a page could use to name tab mechanics. Name is not chrome, it
    /// never renders (the header pin above still finds text and nothing else): it is what this
    /// tab is called in an address, which a translated Title cannot be (nsail#180).</summary>
    [Fact]
    public void NsTab_ExposesOnlyLabelNameAndContent()
    {
        var parameters = typeof(NsTab)
            .GetProperties()
            .Where(p => p.GetCustomAttributes(typeof(Microsoft.AspNetCore.Components.ParameterAttribute), true).Length != 0)
            .Select(p => p.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["ChildContent", "Name", "Title"], parameters);
    }
}
