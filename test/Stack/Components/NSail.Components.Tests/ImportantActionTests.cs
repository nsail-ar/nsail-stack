// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Tones;

namespace NSail.Components.Tests;

/// <summary>The rung below filled (nsail#842). MudBlazor has no tonal variant, so the chrome is
/// an <c>ns-</c> class over the vendor's filled one — which makes two things worth pinning that
/// a render can actually see: the class is there and the FILLED ACCENT is not (a tonal act that
/// still carries <c>mud-button-filled-primary</c> is the very thing the rung exists to stop
/// spending), and a command and a link at the same rung wear the same class rather than each
/// deciding. The fill itself is CSS and bUnit paints nothing, so the stylesheet is read for the
/// one promise a hex would break: that AccentSoft is DERIVED from the accent the tenant picked.
/// The label on it is not in the stylesheet at all — it is a luminance decision, so the theme
/// hands it over as <c>--ns-accent-ink</c> and the render is where that can be seen.</summary>
public sealed class ImportantActionTests : BunitContext
{
    public ImportantActionTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddSingleton(new RouteTable(typeof(ImportantActionTests).Assembly, []));
        Services.AddScoped<RootSurface>();
        Services.AddScoped<SurfaceHistory>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void AnImportantButtonWearsTheWashAndNotTheFilledAccent()
    {
        var cut = Render<NsButton>(p => p
            .Add(x => x.As, NsAs.Important)
            .Add(x => x.Label, "Abrir"));

        var button = cut.Find("button");

        Assert.Contains("ns-important", button.ClassList);
        Assert.Contains("mud-button-filled", button.ClassList);
        Assert.DoesNotContain("mud-button-filled-primary", button.ClassList);
    }

    [Fact]
    public void AnImportantLinkWearsTheSameClassAsTheButton()
    {
        var cut = Render<NsLink>(p => p
            .Add(x => x.As, NsAs.Important)
            .Add(x => x.Href, "/tills/1/close")
            .Add(x => x.Label, "Cerrar"));

        var link = cut.Find("a");

        Assert.Contains("ns-important", link.ClassList);
        Assert.DoesNotContain("mud-button-filled-primary", link.ClassList);
    }

    [Fact]
    public void AMainButtonIsStillTheFilledAccent()
    {
        var cut = Render<NsButton>(p => p
            .Add(x => x.As, NsAs.Main)
            .Add(x => x.Label, "Guardar"));

        var button = cut.Find("button");

        Assert.Contains("mud-button-filled-primary", button.ClassList);
        Assert.DoesNotContain("ns-important", button.ClassList);
    }

    // A stored hex would freeze one tenant's orange into every other tenant's buttons, which is
    // the whole reason this is a mix and not a 21st brand colour: the fill has to be a function
    // of --mud-palette-primary, whatever that resolves to. The tenth is read off BrandTone
    // rather than typed here, because the ink that stands on this wash is measured against the
    // same number in C# — a stylesheet washing at one strength while the ink was computed for
    // another is a contrast guarantee that quietly stops being true.
    [Fact]
    public void TheSoftFillIsDerivedFromTheAccentTheTenantPicked()
    {
        var css = ReadStylesheet();
        var wash = Math.Round(BrandTone.WashWeight * 100, 4).ToString(CultureInfo.InvariantCulture);

        Assert.Contains(
            $"--ns-accent-soft: color-mix(in srgb, var(--mud-palette-primary) {wash}%, transparent);",
            css);

        Assert.Contains("background-color: var(--ns-accent-soft);", css);
    }

    // The label is the one thing on this rung a stylesheet cannot decide: which ink reads on the
    // wash is a question about the accent's own luminance, and color-mix cannot ask it. So the
    // theme computes it and hands it over per lit scheme, and the stylesheet DEFINES it nowhere
    // — a blend left in :root would be the luminance-blind derivation this replaced, still
    // winning for any brand that never mounts a theme.
    [Fact]
    public void TheSoftFillsLabelIsHandedOverByTheThemeAndNotMixedInTheStylesheet()
    {
        var cut = Render<NsTheme>(p => p
            .Add(x => x.Brand, new Brand())
            .Add(x => x.Dark, true));

        Assert.Contains($"--ns-accent-ink:{BrandTheme.DefaultDark().AccentInk};", cut.Markup);
        Assert.DoesNotContain("--ns-accent-ink:", ReadStylesheet());
    }

    // Same brand, other scheme: the wash is the scheme's surface tinted by the accent, so the
    // ink that reads on it is a different colour in each and the variable has to follow the
    // scheme that is lit rather than the brand alone.
    [Fact]
    public void TheLightSchemeGetsItsOwnLabel()
    {
        var cut = Render<NsTheme>(p => p
            .Add(x => x.Brand, new Brand())
            .Add(x => x.Dark, false));

        Assert.Contains($"--ns-accent-ink:{BrandTheme.DefaultLight().AccentInk};", cut.Markup);
        Assert.NotEqual(BrandTheme.DefaultLight().AccentInk, BrandTheme.DefaultDark().AccentInk);
    }

    static string ReadStylesheet()
    {
        return HouseStylesheet.Read();
    }
}
