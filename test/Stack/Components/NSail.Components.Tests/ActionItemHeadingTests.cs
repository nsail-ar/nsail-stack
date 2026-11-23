// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Icons;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>A contributed act names the heading it sits under (ActionItem.Heading), so a
/// presenter that draws a grouped list tells which acts share one and draws its label and glyph
/// without knowing any contributor.</summary>
public sealed class ActionItemHeadingTests
{
    static ActionItem Act(string name, string? heading = null, Glyph? icon = null)
    {
        return new ActionItem { Name = name, Heading = heading, HeadingIcon = icon, OnClick = () => Task.CompletedTask };
    }

    static StringManager Strings(string language)
    {
        var catalog = new StringCatalog([new BilingualStrings()]);

        return new StringManager(catalog, new LanguageProvider { Current = language }, new MetadataProvider());
    }

    [Fact]
    public void ActsThatShareAHeadingStandUnderOne()
    {
        var headings = ActionHeading.Of([Act("Card", "North"), Act("Link", "South"), Act("Cash", "North"), Act("Other")]);

        Assert.Equal(["North", "South"], headings.Select(h => h.Name));
        Assert.Equal(["Card", "Cash"], headings[0].Items.Select(a => a.Name));
        Assert.Equal(["Link"], headings[1].Items.Select(a => a.Name));
    }

    [Fact]
    public void AnActWithNoHeadingStandsUnderNone()
    {
        Assert.Empty(ActionHeading.Of([Act("Card"), Act("Cash")]));
    }

    [Fact]
    public void TheHeadingWearsTheGlyphItsActsSet()
    {
        var heading = Assert.Single(ActionHeading.Of([Act("Card", "North", NsIcons.Sync)]));

        Assert.Equal(NsIcons.Sync, heading.Icon);
    }

    [Theory]
    [InlineData("es", "Norte")]
    [InlineData("en", "North side")]
    public void TheLabelResolvesInTheInstallsLanguage(string language, string label)
    {
        var heading = Assert.Single(ActionHeading.Of([Act("Card", "North")]));

        Assert.Equal(label, heading.GetLabel(Strings(language)));
    }

    [Fact]
    public void AHeadingWithNoStringReadsAsItsName()
    {
        var heading = Assert.Single(ActionHeading.Of([Act("Card", "South")]));

        Assert.Equal("South", heading.GetLabel(Strings("es")));
    }

    [Fact]
    public void ContributorsThatDisagreeOnTheGlyphLeaveTheLastOne()
    {
        var heading = Assert.Single(ActionHeading.Of([Act("Card", "North", NsIcons.Sync), Act("Cash", "North"), Act("Link", "North", NsIcons.Edit)]));

        Assert.Equal(NsIcons.Edit, heading.Icon);
        Assert.Equal(3, heading.Items.Count);
    }

    sealed class BilingualStrings : IStringSource
    {
        public IReadOnlyCollection<string> Languages { get; } = ["es", "en"];

        public Task<IReadOnlyDictionary<string, string>> GetStrings(string language)
        {
            IReadOnlyDictionary<string, string> entries = new Dictionary<string, string>
            {
                ["Actions.Headings.North"] = language == "es" ? "Norte" : "North side",
            };

            return Task.FromResult(entries);
        }
    }
}
