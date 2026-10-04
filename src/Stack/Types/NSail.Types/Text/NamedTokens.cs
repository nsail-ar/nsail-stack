// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text;
using System.Text.RegularExpressions;

namespace NSail.Text;

/// <summary>Named placeholders in a text — "{name}", "{when}" — read out of it or filled in.
/// One implementation, because two would let a text that reads a token be saved beside a
/// filler that does not write it.</summary>
public static partial class NamedTokens
{
    /// <summary>Every token the text names, in the order it first names them and without
    /// repeats — which is the order a positional wire numbers them by.</summary>
    public static IReadOnlyList<string> In(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var names = new List<string>();

        foreach (Match match in Token().Matches(text))
        {
            var name = match.Groups[1].Value;

            if (!names.Contains(name, StringComparer.Ordinal))
            {
                names.Add(name);
            }
        }

        return names;
    }

    public static string Fill(string text, IReadOnlyDictionary<string, string>? values)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (values is null || values.Count == 0 || !text.Contains('{', StringComparison.Ordinal))
        {
            return text;
        }

        var result = new StringBuilder(text);

        foreach (var value in values)
        {
            result.Replace($"{{{value.Key}}}", value.Value);
        }

        return result.ToString();
    }

    [GeneratedRegex(@"\{([A-Za-z][A-Za-z0-9_]*)\}")]
    private static partial Regex Token();
}
