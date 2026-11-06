// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.Json;
using System.Text.RegularExpressions;

namespace NSail.Localization.Testing;

/// <summary>The sweep that keeps River Plate slang out of every <c>strings.es.json</c>: the Stack's
/// tests and a product's Architecture suite both run it, so the list lives only here.</summary>
public static class SpanishCopy
{
    /// <summary>Whole words, or stems that match every inflection.</summary>
    public static IReadOnlyList<string> Slang { get; } =
    [
        "plata", "guita", @"labur\p{L}*", "un toque", "dale", @"cheque(a|á|ar|o)\p{L}*",
        "bárbaro", "quilombo", "mango", "pibe", "boludo", "re lindo",
    ];

    /// <summary>Slang words in <paramref name="text"/>, as their list entries.</summary>
    public static IReadOnlyList<string> Find(string text)
    {
        return Slang
            .Where(word => Regex.IsMatch(text, $@"(?<![\p{{L}}])(?:{word})(?![\p{{L}}])", RegexOptions.IgnoreCase))
            .ToList();
    }

    /// <summary>Every slang word in a <c>strings.es.json</c> under <paramref name="root"/>, as
    /// <c>path: key: word</c>. An assistant profile is a model prompt that may gloss the user's
    /// slang, so its key is skipped.</summary>
    public static IReadOnlyList<string> Offenders(string root)
    {
        var separator = Path.DirectorySeparatorChar;

        return Directory.EnumerateFiles(root, "strings.es.json", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{separator}obj{separator}") && !file.Contains($"{separator}bin{separator}"))
            .SelectMany(file => Strings(file).Select(entry => (file, entry.Key, entry.Value)))
            .Where(entry => !entry.Key.EndsWith(".Assistant.Profile", StringComparison.Ordinal))
            .SelectMany(entry => Find(entry.Value).Select(word => $"{Path.GetRelativePath(root, entry.file)}: {entry.Key}: {word}"))
            .ToList();
    }

    static Dictionary<string, string> Strings(string file)
    {
        return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file)) ?? [];
    }
}
