// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Icons;

namespace NSail.Components.Tests;

/// <summary>The glyph that labels a card's title takes the MUTED rank (nsail#1051, overruling
/// nsail#842's reading): the word is the heading and the glyph names what the card is about
/// behind it, so a home full of headings has one voice leading each. The rank is stated rather
/// than left to the vendor's own default, which resolves the same today, and it is scoped to
/// the title's own box and not to the header, because the header's other tenant is the row of
/// acts at its end (nsail#925) and a button carries its rank's own ink. bUnit paints nothing,
/// so what a render proves is the hook the rule selects by; the rule itself is read off the
/// shipped stylesheet, the CollapsedLabelWhisperTests idiom.</summary>
public sealed class CardIconRankTests : BunitContext
{
    public CardIconRankTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void TheCardHeaderCarriesTheHookTheRankIsWrittenAgainst()
    {
        var cut = Render<NsCard>(p => p
            .Add(x => x.Header, (RenderFragment)(builder =>
            {
                builder.OpenComponent<NsIcon>(0);
                builder.AddComponentParameter(1, nameof(NsIcon.Icon), NsIcons.Search);
                builder.CloseComponent();
            })));

        var title = cut.Find(".ns-card-header .mud-card-header-content");

        // Said once where the header is DEFINED rather than at every card: the glyph is the
        // caller's, the rank is not.
        Assert.NotEmpty(title.QuerySelectorAll(".mud-icon-root"));
    }

    [Fact]
    public void ATitlesGlyphStepsDownToTheMutedRank()
    {
        var css = ReadStylesheet();

        Assert.Contains(
            """
            .ns-card-header .mud-card-header-content .mud-icon-root {
                color: var(--mud-palette-text-secondary);
            }
            """,
            css);
    }

    /// <summary>The grid item carries no ink of its own (nsail#925): the door is a button in
    /// the header's row and takes its own intention's fill and ink, so a muted colour on the
    /// wrapper would reach the card as well as the door — the switched-off heading the rule
    /// above exists to stop.</summary>
    [Fact]
    public void TheDashboardItemPaintsNothing()
    {
        var css = ReadStylesheet();
        var item = css[css.IndexOf(".ns-dashboard-item {", StringComparison.Ordinal)..];
        var rule = item[..item.IndexOf('}', StringComparison.Ordinal)];

        Assert.DoesNotContain("color:", rule, StringComparison.Ordinal);

        // Nothing is positioned against a card any more, so the class that did it is gone
        // rather than left behind matching nothing.
        Assert.DoesNotContain("ns-dashboard-door", css, StringComparison.Ordinal);
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
