// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.RegularExpressions;

namespace NSail.Components.Tests;

/// <summary>A search box's hint is Common.Search unless a caller passes a text, and a text is
/// passed as a key's translation, never typed into the markup — a literal is a word no language
/// file can answer.</summary>
public sealed partial class SearchPlaceholderLiteralTests
{
    [GeneratedRegex("""<(?:NsSearchField\b(?:=>|=|"[^"]*"|[^>"=])*?\bPlaceholder|(?:NsPageHeader|NsNavMenu|NsTree)\b(?:=>|=|"[^"]*"|[^>"=])*?\bSearchPlaceholder)\s*=\s*"[^@"][^"]*"[^>]*>""", RegexOptions.Singleline)]
    private static partial Regex LiteralOnSearchField();

    static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "NSail.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("NSail.sln not found above the test binaries.");
    }

    [Fact]
    public void NoSearchFieldCarriesAPlaceholderLiteral()
    {
        var files = Directory.EnumerateFiles(Path.Combine(Root(), "src"), "*.razor", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .ToList();

        Assert.Contains(files, file => File.ReadAllText(file).Contains("<NsSearchField"));

        var offenders = files
            .Where(file => LiteralOnSearchField().IsMatch(File.ReadAllText(file)))
            .Select(file => Path.GetRelativePath(Root(), file))
            .ToList();

        Assert.True(offenders.Count == 0, $"A search box with a placeholder literal: {string.Join(", ", offenders)}");
    }

    [Theory]
    [InlineData("""<NsSearchField Placeholder="Buscar" />""", true)]
    [InlineData("""<NsSearchField Value="x" Placeholder="Escribí" Grow />""", true)]
    [InlineData("""<NsSearchField Placeholder="@Hint" />""", false)]
    [InlineData("""<NsSearchField Value="x" />""", false)]
    [InlineData("""<NsTextField Placeholder="Buscar" />""", false)]
    [InlineData("""<NsPageHeader Title="x" SearchPlaceholder="Buscar" />""", true)]
    [InlineData("""<NsNavMenu SearchPlaceholder="Buscar" />""", true)]
    [InlineData("""<NsTree Items="x" SearchPlaceholder="Buscar" />""", true)]
    [InlineData("""<NsTree SearchPlaceholder="@Translate("A.B")" />""", false)]
    [InlineData("""<NsTree Items="x" Children="@(n => n.Children)" SearchPlaceholder="Sucursal" />""", true)]
    [InlineData("""<NsPageHeader SearchTextChanged="@(v => Set(v))" SearchPlaceholder="Pedido" />""", true)]
    [InlineData("""<NsSearchField ValueChanged="@((string s) => Set(s))" Placeholder="Buscar" />""", true)]
    [InlineData("""<NsSearchField Value="@(new List<int>())" Placeholder="Buscar" />""", true)]
    [InlineData("""<NsTree Children="@(n => n.Children)" SearchPlaceholder="@Translate("A.B")" />""", false)]
    public void TheScanSeesALiteralAndNothingElse(string markup, bool literal)
    {
        Assert.Equal(literal, LiteralOnSearchField().IsMatch(markup));
    }
}
