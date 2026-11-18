// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.RegularExpressions;
using NSail.Harness.Repositories;

namespace NSail.Harness;

// The harness's own protocol on a PR's thread, in HTML comments people do not see: the gate's verdict
// at a head (`harness:gate:<green|red>:<sha>`, or `harness:gate:waits:<sha>:<main>`) and the reviewer's (`<review marker>:<clean|findings>:<sha>`).
// A marker counts only as a whole HTML comment on its own line, written by the harness's own login or
// by one of the fleet's (`authors`).
public sealed class Markers(string review, IReadOnlyCollection<string>? authors = null)
{
    readonly IReadOnlyCollection<string> _authors = authors ?? [];

    // The commit statuses the harness keeps its verdicts in: the gate (on a PR head and on main), a
    // product's suite and its day's cut tried (on main's head), the last QA drain (on main's head).
    public const string GateContext = "harness/gate";
    public const string QaContext = "harness/qa";

    // A clean read by a reviewer that does not merge (a Review worker with Merge off), on the PR head.
    public const string ReviewContext = "harness/review";

    public static string SuiteContext(string product)
    {
        return $"harness/suite/{product}";
    }

    public static string CutContext(string product)
    {
        return $"harness/cut/{product}";
    }

    // A cut at work: failure (a state the marks read), renewed every beat while a harness runs it; success once it let go.
    public static readonly TimeSpan ClaimBeat = TimeSpan.FromMinutes(10);

    // A claim not renewed for this long belongs to a harness that died.
    public static readonly TimeSpan ClaimLapse = ClaimBeat * 3;

    public static string HeldContext(string product)
    {
        return $"harness/cut/{product}/held";
    }

    // What a dead voyage's comment on the story carries, so the next one can count them.
    public const string Voyage = "<!-- harness:voyage -->";

    // An analysis that ended without a verdict, and a job that broke: counted the same way.
    public const string AnalysisDied = "<!-- harness:analysis-died -->";

    // On the comment that names the parts a story sent to other repositories: one story sends them once.
    public const string Elsewhere = "<!-- harness:elsewhere -->";
    public const string Broke = "<!-- harness:broke -->";

    // On the comment that says a lit andon stands still with nothing of the release to start.
    public const string AndonStill = "<!-- harness:andon-still -->";

    // A red main's story closed as fixed: a close that broke after its comment does not say it twice.
    public const string RedFixed = "<!-- harness:red-fixed -->";

    // On the comment of an analysis that found a story's work already on the default branch: the
    // commits that hold it, as whole shas.
    const string FixOpens = "<!-- harness:fix";

    static readonly Regex FixLine = new(@"^<!-- harness:fix ([0-9a-f]{40}(?:\|[0-9a-f]{40})*) -->[ \t]*\r?$", RegexOptions.Multiline);

    static readonly Regex AnyFix = new(@"<!--\s*harness:fix[^>]*-->");

    public static string Fix(IEnumerable<string> shas)
    {
        return $"{FixOpens} {string.Join('|', shas)} -->";
    }

    // The fix the newest such comment names, written by this harness or one of the fleet as a whole
    // HTML comment on its own line; null with none. Dated by the comment, which is when the story's
    // work was said to be there.
    public NamedFix? FixOf(IEnumerable<Board.StoryComment> comments, string login)
    {
        foreach (var c in comments.OrderByDescending(c => c.At))
        {
            var mine = string.Equals(c.Author, login, StringComparison.OrdinalIgnoreCase) || _authors.Contains(c.Author, StringComparer.OrdinalIgnoreCase);
            if (mine && FixLine.Match(c.Body) is { Success: true } m)
            {
                return new NamedFix(m.Groups[1].Value.Split('|'), c.At);
            }
        }
        return null;
    }

    // A seat's words with no fix marker of their own in them: the harness writes that marker itself,
    // after it checked the commits.
    public static string StripFix(string text)
    {
        return AnyFix.Replace(text, "");
    }

    // A story comment carries the marker when this harness (`login`) or one of the fleet wrote it and the
    // marker is a whole HTML comment on its own line: anyone else's is a forgery.
    public bool Says(Board.StoryComment comment, string marker, string login)
    {
        var mine = string.Equals(comment.Author, login, StringComparison.OrdinalIgnoreCase) || _authors.Contains(comment.Author, StringComparer.OrdinalIgnoreCase);
        return mine && Regex.IsMatch(comment.Body, $@"^{Regex.Escape(marker)}[ \t]*\r?$", RegexOptions.Multiline);
    }

    // How many comments carrying the marker close the thread in a row since a hand lifted a hold:
    // any other word between them (a seat's, a person's) breaks the run.
    public int InARow(IEnumerable<Board.StoryComment> comments, string marker, string login, DateTimeOffset? since = null)
    {
        var run = 0;
        foreach (var c in comments.Where(c => since is null || c.At > since).OrderBy(c => c.At))
        {
            run = Says(c, marker, login) ? run + 1 : 0;
        }
        return run;
    }

    public static string Gate(bool green, string sha)
    {
        return $"<!-- harness:gate:{(green ? "green" : "red")}:{sha} -->";
    }

    // A head red only where main is red: no round owes it, and it is gated again once main moves.
    public static string GateWaits(string sha, string main)
    {
        return $"<!-- harness:gate:waits:{sha}:{main} -->";
    }

    public string Review(string word, string sha)
    {
        return $"<!-- {review}:{word}:{sha} -->";
    }

    // The bodies on a thread that this harness or another of the fleet wrote: a marker from anyone else is a forgery.
    public IEnumerable<string> Bodies(IEnumerable<PrNote> thread, string login)
    {
        return thread.Where(n => string.Equals(n.Author, login, StringComparison.OrdinalIgnoreCase) || _authors.Contains(n.Author, StringComparer.OrdinalIgnoreCase)).Select(n => n.Body);
    }

    // A text with the protocol's markers taken out until none remains (removing one can join the halves of another), for words that are not the harness's own.
    public string Strip(string text)
    {
        text = StripFix(text);
        var marker = new Regex($@"<!--\s*(harness:gate|{Regex.Escape(review)}):[^>]*-->");
        for (var shorter = marker.Replace(text, ""); shorter != text; shorter = marker.Replace(text, ""))
        {
            text = shorter;
        }
        return text;
    }

    // The gate's verdict at a head, the last one written. A head waiting on main reads pending while main is still the head it waited on (none known: still).
    public static CiState? GateAt(IEnumerable<string> bodies, string sha, string? main = null)
    {
        CiState? word = null;
        foreach (var body in bodies)
        {
            foreach (Match m in Regex.Matches(body, @"^<!-- harness:gate:(green|red|waits):([0-9a-f]+)(?::([0-9a-f]+))? -->[ \t]*\r?$", RegexOptions.Multiline))
            {
                if (m.Groups[2].Value != sha) continue;
                word = m.Groups[1].Value switch
                {
                    "green" => CiState.Green,
                    "red" => CiState.Red,
                    "waits" when main is null || main == m.Groups[3].Value => CiState.Pending,
                    _ => null,
                };
            }
        }
        return word;
    }

    // The reviewer's last word at a head (clean, approved: clean from a reviewer that does not merge,
    // findings), null when nobody read it.
    public string? ReviewAt(IEnumerable<string> bodies, string sha)
    {
        string? word = null;
        foreach (var body in bodies)
        {
            foreach (Match m in ReviewMatches(body))
            {
                if (m.Groups[2].Value == sha) word = m.Groups[1].Value;
            }
        }
        return word;
    }

    // The heads a reviewer read with findings, oldest first.
    public IReadOnlyList<string> Findings(IEnumerable<string> bodies)
    {
        return bodies.SelectMany(b => ReviewMatches(b).Where(m => m.Groups[1].Value == "findings").Select(m => m.Groups[2].Value)).Distinct().ToList();
    }

    MatchCollection ReviewMatches(string body)
    {
        return Regex.Matches(body, $@"^<!-- {Regex.Escape(review)}:(clean|approved|findings):([0-9a-f]+) -->[ \t]*\r?$", RegexOptions.Multiline);
    }
}
