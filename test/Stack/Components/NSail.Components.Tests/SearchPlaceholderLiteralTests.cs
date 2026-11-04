// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.RegularExpressions;

namespace NSail.Components.Tests;

/// <summary>A search box's hint is Common.Search unless a caller passes a text, and a text is
/// passed as a key's translation, never typed into the markup — a literal is a word no language
/// file can answer.</summary>
public sealed partial class SearchPlaceholderLiteralTests
{
    [GeneratedRegex("""<NsSearchField\b[^>]*?\bPlaceholder\s*=\s*"[^@"][^"]*"[^>]*>""", RegexOptions.Singleline)]
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
        var offenders = Directory.EnumerateFiles(Path.Combine(Root(), "src"), "*.razor", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(file => LiteralOnSearchField().IsMatch(File.ReadAllText(file)))
            .Select(file => Path.GetRelativePath(Root(), file))
            .ToList();

        Assert.True(offenders.Count == 0, $"NsSearchField with a Placeholder literal: {string.Join(", ", offenders)}");
    }

    [Theory]
    [InlineData("""<NsSearchField Placeholder="Buscar" />""", true)]
    [InlineData("""<NsSearchField Value="x" Placeholder="Escribí" Grow />""", true)]
    [InlineData("""<NsSearchField Placeholder="@Hint" />""", false)]
    [InlineData("""<NsSearchField Value="x" />""", false)]
    [InlineData("""<NsTextField Placeholder="Buscar" />""", false)]
    public void TheScanSeesALiteralAndNothingElse(string markup, bool literal)
    {
        Assert.Equal(literal, LiteralOnSearchField().IsMatch(markup));
    }
}
