// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.RegularExpressions;

namespace NSail.Harness.Jobs;

// What the harness trusts of the files a seat wrote: nothing it can check is taken on the seat's word.
public static class SeatOutput
{
    // The labels of a seat's list the board knows and a seat may set.
    public static IReadOnlyList<string> AllowedLabels(IEnumerable<string> asked, IEnumerable<string> known)
    {
        var set = known.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return asked.Select(l => l.Trim()).Where(l => l.Length > 0 && set.Contains(l)
            && !Labels.SeatForbidden.Contains(l, StringComparer.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    // The changed files that match a protected glob.
    public static IReadOnlyList<string> Protected(IEnumerable<string> files, IEnumerable<string> globs)
    {
        var rx = globs.Select(Glob).ToList();
        return files.Select(f => f.Trim().Replace('\\', '/')).Where(f => f.Length > 0 && rx.Any(r => r.IsMatch(f))).ToList();
    }

    static Regex Glob(string glob)
    {
        var g = glob.Trim().Replace('\\', '/').TrimStart('/');
        var rx = "^" + Regex.Escape(g).Replace(@"\*\*/", "(.*/)?").Replace(@"\*\*", ".*").Replace(@"\*", "[^/]*").Replace(@"\?", "[^/]") + "$";
        return new Regex(rx, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
