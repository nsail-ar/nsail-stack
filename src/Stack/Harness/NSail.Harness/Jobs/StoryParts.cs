// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using System.Text.RegularExpressions;

namespace NSail.Harness.Jobs;

// One story an analyst's split asks for: Comment is its refinery comment (none: it is analyzed as any
// filed story), Protected the paths its change must touch.
public sealed record StoryPart(string Title, string Body, IReadOnlyList<string> Labels, string Comment, string Protected);

// The parts an analyst leaves under `parts/<n>/` of its out folder (`title.txt`, `body.md`, and where
// it has them `labels.txt`, `comment.md`, `protected.txt`), numbered from 1 with no gap. A part
// names an earlier one as `__PART_<n>__`, which becomes that story's reference once it is filed: the
// seat cannot know a number the board has not given yet.
public static partial class StoryParts
{
    // Fewer is no split; more is a plan, which a hand files.
    public const int Fewest = 2;
    public const int Most = 6;

    [GeneratedRegex(@"__PART_(?<n>\d+)__", RegexOptions.CultureInvariant)]
    private static partial Regex Token();

    // The parts in order, or null when what the seat left is not a split the harness can file whole.
    public static IReadOnlyList<StoryPart>? Read(string outDir)
    {
        var root = Path.Combine(outDir, "parts");
        if (!Directory.Exists(root)) return null;
        var count = Directory.GetDirectories(root).Length;
        if (count is < Fewest or > Most) return null;
        var parts = new List<StoryPart>();
        for (var i = 1; i <= count; i++)
        {
            var dir = Path.Combine(root, i.ToString(CultureInfo.InvariantCulture));
            if (!Directory.Exists(dir)) return null;
            string Text(string file)
            {
                var path = Path.Combine(dir, file);
                return File.Exists(path) ? File.ReadAllText(path) : "";
            }
            var title = Text("title.txt").Trim();
            var body = Text("body.md");
            var comment = Text("comment.md");
            if (title.Length == 0 || title.Contains('\n') || Token().IsMatch(title) || body.Trim().Length == 0) return null;
            if (Named($"{body}\n{comment}").Any(n => n < 1 || n >= i)) return null;
            parts.Add(new StoryPart(title, body, Text("labels.txt").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), comment, Text("protected.txt")));
        }
        return parts;
    }

    // The text with each `__PART_<n>__` replaced by the reference of the n-th story filed.
    public static string Resolve(string text, IReadOnlyList<int> filed)
    {
        return Token().Replace(text, m => Number(m) is var n && n >= 1 && n <= filed.Count ? $"#{filed[n - 1]}" : m.Value);
    }

    static IEnumerable<int> Named(string text)
    {
        return Token().Matches(text).Select(Number);
    }

    static int Number(Match m)
    {
        return int.TryParse(m.Groups["n"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }
}
