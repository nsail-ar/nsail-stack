// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using System.Text.RegularExpressions;

namespace NSail.Harness;

// How a story is named outside its board: `{key}#{n}` in text and PR titles, `{prefix}/{key}-{n}`
// for its branches (each repository's own prefix). The key is the board's (HarnessOptions.Key).
public sealed class StoryNames(string key, IReadOnlyCollection<string> prefixes)
{
    public string Key { get; } = key;

    public string Ref(int story)
    {
        return $"{Key}#{story}";
    }

    public string PrTitle(int story, string title)
    {
        return $"{Ref(story)}: {Untitled(title)}";
    }

    public string Branch(string prefix, int story)
    {
        return $"{prefix}/{Key}-{story}";
    }

    // A title without a leading `{key}#{n}:`, as a seat may write one.
    public string Untitled(string title)
    {
        return Regex.Replace(title.Trim(), $@"^{Regex.Escape(Key)}#\d+:\s*", "");
    }

    // The story a PR is for: titled `{key}#{n}:`, or headed `{prefix}/{key}-{n}` (a dash suffix allowed).
    public int? StoryOf(string title, string head)
    {
        var t = Regex.Match(title, $@"^{Regex.Escape(Key)}#(\d+):");
        if (t.Success && int.TryParse(t.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var n)) return n;
        return BranchStory(head);
    }

    public int? BranchStory(string head)
    {
        foreach (var prefix in prefixes)
        {
            var h = Regex.Match(head, $@"^{Regex.Escape(prefix)}/{Regex.Escape(Key)}-(\d+)(-|$)");
            if (h.Success && int.TryParse(h.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var n)) return n;
        }
        return null;
    }
}
