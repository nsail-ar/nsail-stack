// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text;
using System.Text.RegularExpressions;
using NSail.Harness.Planning;

namespace NSail.Harness.Hosting;

// What the last pass planned and what it holds back, past the few rows the status page shows:
// /plan, searched and a page at a time. The plan and the held notes page on their own, so reading
// the notes does not throw a person back to the first page of the plan.
static partial class PlanPage
{
    internal const int Glance = 10;
    internal const int PageSize = 25;

    [GeneratedRegex(@"^#(\d+): stalled in (\w+), not Analyze")]
    private static partial Regex HeldNote();

    // The release andon, above the plan: which product, since when, what it waits for.
    internal static void Banner(StringBuilder html, HarnessOptions o, IReadOnlyList<Andon> lit, DateTimeOffset? stillSince, DateTimeOffset now)
    {
        var still = stillSince is { } idle && now - idle >= HarnessLoop.AndonStill
            ? $"<br><b>Standing still since {StatusPage.Local(idle):HH:mm} ({StatusPage.Time((now - idle).TotalSeconds)}): nothing of it to start, nothing running.</b>"
            : "";
        foreach (var a in lit)
        {
            var since = a.Since is { } at ? $" since {StatusPage.Local(at):yyyy-MM-dd HH:mm} ({StatusPage.Time((now - at).TotalSeconds)})" : "";
            html.Append($"<div style=\"background:var(--badbg);color:var(--bad);border-radius:8px;padding:10px 14px;margin:10px 0\"><b>Release andon — {StatusPage.Esc(a.Product)}</b>{StatusPage.Esc(since)}<br>Only its fix and its cut start; everything else waits behind them.<br>Waits for: {StatusPage.Linked(o, a.Waits)}{still}</div>");
        }
    }

    // The status page's plan: the first rows, what is left counted by tier, and the way to the rest.
    internal static void Summary(StringBuilder html, HarnessOptions o, Plan plan)
    {
        html.Append($"<div class=logbar><h2>Plan ({plan.Items.Count})</h2><a href=\"/plan\" style=\"font-size:13px\">All {plan.Items.Count}, with search ›</a></div>");
        if (plan.Items.Count > 0)
        {
            Table(html, o, plan.Items.Select((item, i) => (Rank: i + 1, Item: item)).Take(Glance));
            if (plan.Items.Count > Glance)
            {
                var rest = plan.Items.Skip(Glance).GroupBy(item => item.Tier).Select(g => $"{g.Count()} {StatusPage.Esc(g.Key)}");
                html.Append($"<p class=muted style=\"font-size:12px\">and {plan.Items.Count - Glance} more: {string.Join(" · ", rest)}</p>");
            }
        }

        var held = Held(plan.Notes);
        if (held.Count == 0)
        {
            return;
        }

        html.Append($"<div class=logbar><h2>Held ({plan.Notes.Count})</h2><a href=\"/plan\" style=\"font-size:13px\">All, with search ›</a></div>");
        foreach (var line in held.Take(Glance))
        {
            html.Append($"<p class=muted style=\"font-size:12px\">{line}</p>");
        }

        if (held.Count > Glance)
        {
            html.Append($"<p class=muted style=\"font-size:12px\">and {held.Count - Glance} more lines</p>");
        }
    }

    // A stalled story is one note each; they read as one line per stage, the first few named. Any other
    // note stays a line of its own.
    internal static List<string> Held(IReadOnlyList<string> notes)
    {
        var parsed = notes.Select(n => (Note: n, Match: HeldNote().Match(n))).ToList();
        var lines = parsed.Where(h => !h.Match.Success).Select(h => StatusPage.Esc(h.Note)).ToList();

        foreach (var stage in parsed.Where(h => h.Match.Success).GroupBy(h => h.Match.Groups[2].Value))
        {
            var stories = stage.Select(h => $"#{h.Match.Groups[1].Value}").ToList();
            var more = stories.Count > Glance ? $" and {stories.Count - Glance} more" : "";
            lines.Add($"{stories.Count} stalled in {StatusPage.Esc(stage.Key)}, a hand moves them: {string.Join(" ", stories.Take(Glance))}{more}");
        }

        return lines;
    }

    public static string List(HarnessOptions o, Plan? plan, string? q, string? tier, int? page, int? held)
    {
        var html = JobsPage.Head("Plan");
        html.Append($"<p class=muted style=\"font-size:13px\"><a href=\"/\">← Status</a></p><h1>Plan <small class=muted>{StatusPage.Esc(o.Machine)} · what the last pass planned</small></h1>");
        if (plan is null)
        {
            html.Append("<p class=muted>No pass has run yet.</p>");
            return html.ToString();
        }

        var tiers = plan.Items.Select(item => item.Tier).Distinct().Order().ToList();
        html.Append("<form class=filters method=get action=\"/plan\">");
        html.Append($"<input name=q placeholder=\"Search\" size=24 value=\"{StatusPage.Esc(q ?? "")}\">");
        html.Append(JobsPage.Select("tier", "Every tier", tiers.Select(t => (t, t)), tier));
        html.Append("<button>Filter</button>");
        if (!string.IsNullOrEmpty(q) || !string.IsNullOrEmpty(tier))
        {
            html.Append("<a href=\"/plan\">Clear</a>");
        }

        html.Append("</form>");

        var items = plan.Items
            .Select((item, i) => (Rank: i + 1, Item: item))
            .Where(r => string.IsNullOrEmpty(tier) || r.Item.Tier == tier)
            .Where(r => Matches(q, r.Item.ToString(), r.Item.Tier, r.Item.Why))
            .ToList();
        var at = Pager.Clamp(page, items.Count, PageSize);

        html.Append($"<h2>Plan ({plan.Items.Count})</h2>");
        if (items.Count == 0)
        {
            html.Append("<p class=muted>Nothing in the plan matches.</p>");
        }
        else
        {
            Table(html, o, items.Skip((at - 1) * PageSize).Take(PageSize));
            Pager.Render(html, at, items.Count, PageSize, "item", p => Query(q, tier, p, held), "‹ Previous", "Next ›");
        }

        var notes = plan.Notes.Where(n => Matches(q, n)).ToList();
        if (plan.Notes.Count == 0)
        {
            return html.ToString();
        }

        var heldAt = Pager.Clamp(held, notes.Count, PageSize);
        html.Append($"<h2>Held ({plan.Notes.Count})</h2>");
        if (notes.Count == 0)
        {
            html.Append("<p class=muted>No held story matches.</p>");
            return html.ToString();
        }

        foreach (var note in notes.Skip((heldAt - 1) * PageSize).Take(PageSize))
        {
            html.Append($"<p class=muted style=\"font-size:12px;margin:3px 0\">{StatusPage.Linked(o, note)}</p>");
        }

        Pager.Render(html, heldAt, notes.Count, PageSize, "note", p => Query(q, tier, page, p), "‹ Previous", "Next ›");
        return html.ToString();
    }

    static void Table(StringBuilder html, HarnessOptions o, IEnumerable<(int Rank, WorkItem Item)> rows)
    {
        html.Append("<table class=r><tr><th class=n>#</th><th>Tier</th><th>Work</th></tr>");
        foreach (var (rank, item) in rows)
        {
            html.Append($"<tr><td class=n>{rank}</td><td><span class=b>{StatusPage.Esc(item.Tier)}</span></td><td>{StatusPage.Linked(o, item.ToString())}</td></tr>");
        }

        html.Append("</table>");
    }

    internal static bool Matches(string? q, params string[] fields)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return true;
        }

        return q.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .All(word => fields.Any(f => f.Contains(word, StringComparison.OrdinalIgnoreCase)));
    }

    static string Query(string? q, string? tier, int? page, int? held)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(q))
        {
            parts.Add($"q={Uri.EscapeDataString(q)}");
        }

        if (!string.IsNullOrEmpty(tier))
        {
            parts.Add($"tier={Uri.EscapeDataString(tier)}");
        }

        if (page is > 1)
        {
            parts.Add($"page={page}");
        }

        if (held is > 1)
        {
            parts.Add($"held={held}");
        }

        return parts.Count == 0 ? "/plan" : "/plan?" + string.Join("&amp;", parts);
    }
}
