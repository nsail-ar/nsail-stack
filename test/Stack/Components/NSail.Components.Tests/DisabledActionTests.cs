// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using MudBlazor.Utilities;
using NSail.Tones;

namespace NSail.Components.Tests;

/// <summary>The refused act, and neither half of it may reach the accent. A muted fill under a
/// warm label is <c>NsAs.Important</c> — a live act at low emphasis — so an inert ink or fill
/// derived from the tenant's pick makes a disabled control the same picture as that rung. The
/// ink is a scheme constant, asserted here to be what the palette hands over for any pick; the
/// fill is a stylesheet mix of the scheme's INK, which is what the stylesheet is read for.</summary>
public sealed class DisabledActionTests
{
    // The picks BrandThemeContrastTests sweeps the tonal label over, named again here for the
    // opposite promise: that one thing on the screen does NOT move when they do.
    public static TheoryData<string> Picks =>
        ["#F68E1E", "#000000", "#ffffff", "#f6ff00", "#f7c9d5", "#bfe3d0", "#2a3f84"];

    [Theory]
    [MemberData(nameof(Picks))]
    public void TheRefusedInkIsTheSchemesOwnAndNeverTheAccents(string accent)
    {
        var brand = new Brand();

        brand.Dark.Accent = accent;
        brand.Light.Accent = accent;

        var theme = BrandMudTheme.From(brand);

        Assert.Equal(new MudColor(BrandTheme.DefaultDark().TextDisabled).Value, theme.PaletteDark.ActionDisabled.Value);
        Assert.Equal(new MudColor(BrandTheme.DefaultLight().TextDisabled).Value, theme.PaletteLight.ActionDisabled.Value);
    }

    // The status channel's neutral is a LIVE state — work still moving, history — and a refusal
    // is not a state of the data at all, so the two may never be one ink. They are kept apart
    // here because the shape invites the merge: both want a grey, and one value in both places
    // makes "nothing to report" and "you cannot press this" the same word on screen. The
    // neutral's own token is the muted rank (NsStatusToneTests); this is the distance between
    // that rank and the rung below it, in the palette rather than in the stylesheet.
    [Fact]
    public void TheStatusChannelsNeutralIsNotTheRefusedInk()
    {
        var theme = BrandMudTheme.From(new Brand());

        Assert.NotEqual(theme.PaletteDark.ActionDisabled.Value, theme.PaletteDark.TextSecondary.Value);
        Assert.NotEqual(theme.PaletteLight.ActionDisabled.Value, theme.PaletteLight.TextSecondary.Value);
    }

    // Both vendor slots for a refused control carry the ladder's third rung. The vendor styles
    // some refusals from one and some from the other — a disabled menu row reads text-disabled,
    // a disabled icon button action-disabled — so a rung that is the brand's in one slot and
    // the vendor's own translucent grey in the other is two refusals on one screen.
    [Fact]
    public void BothRefusedSlotsCarryTheSameRung()
    {
        var theme = BrandMudTheme.From(new Brand());

        Assert.Equal(theme.PaletteDark.ActionDisabled.Value, theme.PaletteDark.TextDisabled.Value);
        Assert.Equal(theme.PaletteLight.ActionDisabled.Value, theme.PaletteLight.TextDisabled.Value);
    }

    // A stored hex would be one tenant's, and a mix of --mud-palette-primary would be that
    // collision itself: the fill has to be a function of the scheme's INK, so it is neutral
    // for a warm brand and for a cold one alike. The share is read off BrandTone rather than
    // typed here for the reason its sibling's is (ImportantActionTests): the ground a refused
    // label stands on is measured against the same number in C#, and a stylesheet filling at one
    // strength while the reading was taken at another is a guarantee that quietly stops holding.
    [Fact]
    public void TheRefusedFillIsDerivedFromTheSchemesInkAndNotFromTheAccent()
    {
        var css = ReadStylesheet();
        var share = Math.Round(BrandTone.InertWeight * 100, 4).ToString(CultureInfo.InvariantCulture);

        Assert.Contains(
            $"--ns-inert-soft: color-mix(in srgb, var(--mud-palette-text-primary) {share}%, transparent);",
            css);

        Assert.DoesNotContain("--ns-inert-soft: color-mix(in srgb, var(--mud-palette-primary)", css);
    }

    // The vendor declares its own disabled background !important, so the house's rule carries
    // one too or it never lands — and it lands on the FILLED face alone: the flat rung and every
    // icon button have no fill to quiet, and are refused by the ink they share with this.
    [Fact]
    public void EveryFilledButtonWearsTheRefusedFillWhenItIsDisabled()
    {
        Assert.Contains(
            ".mud-button-filled:disabled {\n    background-color: var(--ns-inert-soft) !important;\n}",
            ReadStylesheet().Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    static string ReadStylesheet()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NSail.sln")))
            {
                return File.ReadAllText(Path.Combine(
                    directory.FullName,
                    "src/Stack/Components/NSail.Components.Mud/wwwroot/ns-mud.css"));
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"NSail.sln was not found above {AppContext.BaseDirectory}.");
    }
}
