// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.RegularExpressions;

namespace NSail.Localization.Tests;

/// <summary>The Spanish catalogs are neutral: a River Plate slang word cannot return. The list
/// is the one in doctrine/ui/copy.md.</summary>
public sealed class SpanishCopyTests
{
    private static readonly string[] Slang =
    [
        "plata", "guita", "laburo", "laburar", "un toque", "dale", "chequear", "chequeá", "chequeo",
        "bárbaro", "quilombo", "mango", "pibe", "boludo", "re lindo",
    ];

    static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "NSail.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("NSail.sln not found above the test binaries.");
    }

    static bool Contains(string text, string word)
    {
        return Regex.IsMatch(text, $@"(?<![\p{{L}}]){Regex.Escape(word)}(?![\p{{L}}])", RegexOptions.IgnoreCase);
    }

    [Fact]
    public void NoSpanishStringCarriesSlang()
    {
        var files = Directory.EnumerateFiles(Path.Combine(Root(), "src"), "strings.es.json", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .ToList();

        Assert.NotEmpty(files);

        var offenders = files
            .SelectMany(file => File.ReadLines(file).Select(line => (file, line)))
            .SelectMany(entry => Slang.Where(word => Contains(entry.line, word)).Select(word => $"{Path.GetRelativePath(Root(), entry.file)}: {word}"))
            .Distinct()
            .ToList();

        Assert.True(offenders.Count == 0, $"Slang in a Spanish catalog: {string.Join("; ", offenders)}");
    }

    [Theory]
    [InlineData("la plata", true)]
    [InlineData("platales", false)]
    [InlineData("Dale, seguí", true)]
    [InlineData("Validale", false)]
    public void TheScanMatchesWholeWords(string text, bool slang)
    {
        Assert.Equal(slang, Slang.Any(word => Contains(text, word)));
    }
}
