// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>Proves the generic tab-badge mechanism (bc64a60, NsTabKeepAliveTests) holds for
/// BrandingPage's own three-tab split (Claro/Oscuro/Impresión): a validation error on the
/// inactive "Oscuro" tab still blocks submit and marks that tab's badge, and the "Impresión"
/// tab carries no color-shaped field alongside its logo, mirroring the no-colors-in-print
/// mandate structurally.</summary>
public sealed class BrandingTabsTests : BunitContext
{
    public BrandingTabsTests()
    {
        Services.AddMudServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, Fixtures.NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task ValidationErrorOnInactiveDarkTab_BlocksSubmitAndMarksItsTab()
    {
        var model = new BrandingTabsModel { LightPrimary = "#F68E1E", DarkPrimary = null, PrintLogoAssetId = "asset-1" };
        var submitted = false;

        var cut = Render<BrandingTabsHost>(p => p
            .Add(x => x.Model, model)
            .Add(x => x.Submitted, () => submitted = true));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.False(submitted);

        var tabHeaders = cut.FindAll(".mud-tab");
        Assert.Equal(3, tabHeaders.Count);

        // Claro (0): valid. Oscuro (1): the inactive tab carrying the Required violation.
        // Impresión (2): valid, and its own content never rendered a color field to begin with.
        Assert.DoesNotContain("mud-badge", tabHeaders[0].OuterHtml);
        Assert.Contains("mud-badge-dot", tabHeaders[1].OuterHtml);
        Assert.DoesNotContain("mud-badge", tabHeaders[2].OuterHtml);
    }

    [Fact]
    public async Task FixingTheDarkTabField_UnblocksSubmit()
    {
        var model = new BrandingTabsModel { LightPrimary = "#F68E1E", DarkPrimary = "#F68E1E", PrintLogoAssetId = "asset-1" };
        var submitted = false;

        var cut = Render<BrandingTabsHost>(p => p
            .Add(x => x.Model, model)
            .Add(x => x.Submitted, () => submitted = true));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.True(submitted);
    }
}
