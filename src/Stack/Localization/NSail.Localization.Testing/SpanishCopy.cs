// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NSail.Localization.Testing;

/// <summary>The sweep that keeps River Plate slang out of every <c>strings.es.json</c>: the Stack's
/// tests and a product's Architecture suite both run it, so the list lives only here.</summary>
public static class SpanishCopy
{
    const string Gendered = "[oa]s?";

    // A verb's endings after its stem, an enclitic pronoun optional: chequealo, laburando, chequeé.
    const string Verb = "(?:a|as|an|e|es|en|o|os|amos|emos|ar|aba|abas|aban|aste|aron|ado|ada|ados|adas|ando|ante|antes)(?:lo|la|los|las|le|les|me|te|se|nos)?";

    /// <summary>Each entry is a word with its bounded endings, written without accents: a text is
    /// matched whole-word with its accents folded, so <c>bárbara</c> and <c>barbara</c> both hit
    /// and <c>plataforma</c> does not.</summary>
    public static IReadOnlyList<string> Slang { get; } =
    [
        "platas?", $"guit{Gendered}", $"labur{Verb}", "un toque", "dale", $"cheque{Verb}",
        $"barbar{Gendered}", $"quilomb{Gendered}", "mangos?", "pib[ea]s?", $"bolud{Gendered}",
        $"re lind{Gendered}",
    ];

    static readonly Regex Pattern = new(
        $@"(?<!\p{{L}})(?:{string.Join("|", Slang)})(?!\p{{L}})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>The slang words in <paramref name="text"/>, as written there.</summary>
    public static IReadOnlyList<string> Find(string text)
    {
        var (folded, origin) = Fold(text);

        return Pattern.Matches(folded)
            .Select(match => text[origin[match.Index]..(origin[match.Index + match.Length - 1] + 1)])
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

    // The text without its accents, and for each of its characters the index it came from.
    static (string Folded, List<int> Origin) Fold(string text)
    {
        var folded = new StringBuilder(text.Length);
        var origin = new List<int>(text.Length);

        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsSurrogate(text[i]))
            {
                folded.Append(text[i]);
                origin.Add(i);
                continue;
            }

            foreach (var c in text[i].ToString().Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    folded.Append(c);
                    origin.Add(i);
                }
            }
        }

        return (folded.ToString(), origin);
    }

    static Dictionary<string, string> Strings(string file)
    {
        return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file)) ?? [];
    }
}
