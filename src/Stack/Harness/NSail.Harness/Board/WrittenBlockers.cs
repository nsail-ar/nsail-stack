// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using System.Text.RegularExpressions;

namespace NSail.Harness.Board;

// The blockers a story names in words: only a link the board keeps holds a story, so one written in
// prose — `blocked by #12`, `waits on #12`, `waits for #12`, `depends on #12`, or a `Waits:` line —
// would let it be built before what it needs has landed. A `Waits` line that answers nothing
// (`nothing`, `none`, `nada`, `ninguna`, `ninguno`) names no blocker, whatever else it mentions. A
// reference to another repository (`other#12`) is not this board's to link.
public static partial class WrittenBlockers
{
    // The rest of the clause after the phrase holds its references.
    [GeneratedRegex(@"\b(?:blocked\s+by|waits?\s+(?:on|for)|depends\s+on)\b(?<refs>[^.\n;]*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Phrase();

    [GeneratedRegex(@"^\W*waits\b(?<refs>.*)$", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex WaitsLine();

    [GeneratedRegex(@"^\W*waits\W*(?:nothing|none|nada|ninguna|ninguno)\b.*$", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex NothingLine();

    [GeneratedRegex(@"(?<![\w/#])(?<key>[\w.-]*)#(?<n>\d+)\b", RegexOptions.CultureInvariant)]
    private static partial Regex Reference();

    // The stories the text names as blockers, once each, in order: those with no key or this board's.
    public static IReadOnlyList<int> Named(string text, string key)
    {
        var nothing = NothingLine().Matches(text).ToList();
        bool OffANothingLine(Match m)
        {
            return !nothing.Any(n => m.Index >= n.Index && m.Index < n.Index + n.Length);
        }
        var refs = Phrase().Matches(text).Where(OffANothingLine).Select(m => m.Groups["refs"].Value)
            .Concat(WaitsLine().Matches(text).Where(OffANothingLine).Select(m => m.Groups["refs"].Value));
        return refs.SelectMany(r => Reference().Matches(r))
            .Where(m => m.Groups["key"].Value.Length == 0 || string.Equals(m.Groups["key"].Value, key, StringComparison.OrdinalIgnoreCase))
            .Select(m => int.TryParse(m.Groups["n"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0)
            .Where(n => n > 0)
            .Distinct()
            .ToList();
    }

    // Links every blocker the text names that the board does not link yet, and holds the story
    // (`blocked`) while any named blocker has not landed. Answers the blockers still open.
    public static async Task<IReadOnlyList<int>> Link(IBoard board, int story, string text, string key, CancellationToken ct = default)
    {
        var named = Named(text, key).Where(n => n != story).ToList();
        if (named.Count == 0) return [];
        var linked = (await board.BlockedBy(story, ct)).ToHashSet();
        var open = new List<int>();
        foreach (var n in named)
        {
            try
            {
                if (!linked.Contains(n)) await board.Block(story, n, ct);
                var blocker = await board.Story(n, ct);
                if (!blocker.Open) continue;
                var stage = await board.StageOf(n, ct);
                if (!Stages.Landed.Contains(stage)) open.Add(n);
            }
            // A number that names no story (a PR, a typo) blocks nothing.
            catch (ProviderException)
            {
            }
        }
        if (open.Count > 0) await board.AddLabels(story, Labels.Blocked);
        return open;
    }
}
