// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using MudBlazor;
using MudBlazor.Utilities;
using NSail.Components;

namespace NSail.Components.Tests;

/// <summary>MudBlazor's own overlays (Confirm/Alert, DialogManager.Open's NsOpenDialog host,
/// a select's popover, a snackbar) never joined SurfaceContext's escalation chain -- they
/// used MudBlazor's own defaults, which happened to sit below an aside or a modal. This pins
/// the fix: BrandMudTheme reads SurfaceContext.VendorZIndex for every one of them, so the
/// two can never drift apart again the way a hardcoded 1400 and a chain-derived 1500 did.</summary>
public sealed class BrandMudThemeTests
{
    [Fact]
    public void VendorOverlays_all_read_SurfaceContext_VendorZIndex()
    {
        var theme = BrandMudTheme.From(new Brand());

        Assert.Equal(SurfaceContext.VendorZIndex, theme.ZIndex.Dialog);
        Assert.Equal(SurfaceContext.VendorZIndex, theme.ZIndex.Popover);
        Assert.Equal(SurfaceContext.VendorZIndex, theme.ZIndex.Snackbar);
        Assert.Equal(SurfaceContext.VendorZIndex, theme.ZIndex.Tooltip);
    }

    // Where the derived chrome has to land for the eye to see it (nsail#484): the app bar and
    // the drawer both paint from the Chrome slot, in BOTH schemes. The derivation itself is
    // the Branding kit's, at save -- what this layer owes is that the slot it feeds is the one
    // the shell wears.
    [Fact]
    public void AppbarAndDrawer_paint_from_Chrome_in_both_schemes()
    {
        var brand = new Brand();

        brand.Light.Chrome = "#d8e8d1";
        brand.Dark.Chrome = "#1c2c12";

        var theme = BrandMudTheme.From(brand);

        Assert.Equal(new MudColor("#d8e8d1").Value, theme.PaletteLight.AppbarBackground.Value);
        Assert.Equal(new MudColor("#d8e8d1").Value, theme.PaletteLight.DrawerBackground.Value);

        Assert.Equal(new MudColor("#1c2c12").Value, theme.PaletteDark.AppbarBackground.Value);
        Assert.Equal(new MudColor("#1c2c12").Value, theme.PaletteDark.DrawerBackground.Value);
    }

    // nsail#731: the band's text is the chrome's own ink and never the content's. A light
    // chrome under the dark scheme drew the dark scheme's white TextPrimary on itself for as
    // long as this slot read TextPrimary, which is a white sentence on a pale bar.
    [Fact]
    public void AppbarAndDrawer_read_the_chromes_own_ink_and_never_the_contents()
    {
        var brand = new Brand();

        brand.Dark.Chrome = "#f2f0ec";
        brand.Dark.TextOnChrome = "#26241f";
        brand.Dark.TextOnChromeSecondary = "#6b6a66";

        var theme = BrandMudTheme.From(brand);

        Assert.Equal(new MudColor("#26241f").Value, theme.PaletteDark.AppbarText.Value);
        Assert.Equal(new MudColor("#26241f").Value, theme.PaletteDark.DrawerText.Value);
        Assert.Equal(new MudColor("#6b6a66").Value, theme.PaletteDark.DrawerIcon.Value);

        Assert.NotEqual(theme.PaletteDark.TextPrimary.Value, theme.PaletteDark.DrawerText.Value);
        Assert.NotEqual(theme.PaletteDark.TextSecondary.Value, theme.PaletteDark.DrawerIcon.Value);
    }

    // nsail#2361: a brand that picks nothing keeps the house typeface -- Typography.H6 stays
    // at the vendor's own unset FontFamily, which inherits the body's rather than naming one.
    [Fact]
    public void NoTypeface_leaves_heading_FontFamily_unset()
    {
        var theme = BrandMudTheme.From(new Brand());

        Assert.Null(theme.Typography.H6.FontFamily);
        Assert.Null(theme.Typography.H1.FontFamily);
    }

    // The pick paints the titles -- NsText's own "Title" treatment is Typo.h6, and the brand
    // name renders through it too (BrandingBar) -- and nothing below the heading ranks: Body1
    // (tables, fields, figures) stays the vendor's own default.
    [Fact]
    public void Typeface_sets_heading_FontFamily_and_leaves_body_alone()
    {
        var brand = new Brand { Typeface = "lora" };

        var theme = BrandMudTheme.From(brand);

        Assert.Equal(Typefaces.Find("lora")!.FontFamily, theme.Typography.H6.FontFamily);
        Assert.Equal(Typefaces.Find("lora")!.FontFamily, theme.Typography.H1.FontFamily);
        Assert.NotEqual(Typefaces.Find("lora")!.FontFamily, theme.Typography.Body1.FontFamily);
    }

    [Fact]
    public void UnknownTypefaceKey_falls_back_to_the_house_typeface()
    {
        var theme = BrandMudTheme.From(new Brand { Typeface = "does-not-exist" });

        Assert.Null(theme.Typography.H6.FontFamily);
    }
}
