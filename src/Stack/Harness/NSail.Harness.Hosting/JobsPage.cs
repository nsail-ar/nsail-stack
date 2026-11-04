// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text;
using NSail.Harness.Lanes;

namespace NSail.Harness.Hosting;

// The jobs this machine ran: one table the status page and /jobs share (when, role, work, result,
// outcome, time, an info icon to the job), /jobs with its filters and pages, and /jobs/{id}, one job
// and its log. Plain HTML: a filter is a form, a page a link; nothing but the job's own page, while
// it runs, refreshes by itself, so a person paging the history is not thrown back to the first page.
static class JobsPage
{
    const int PageSize = 25;
    static readonly string[] Results = ["ok", "failed", "broke", "quiet", "running"];
    static readonly int[] Tails = [200, 1000, 0];

    // A row as the table shows it: a job running or ended.
    internal sealed record Row(string Id, DateTimeOffset Started, string Work, int? Story, string Result, string Outcome, string? Took);

    internal static Row RowOf(JobRecord j)
    {
        var took = j.Ended is { } ended ? StatusPage.Time((ended - j.Started).TotalSeconds) : null;
        return new Row(j.Id, j.Started, j.Work, j.Story, ResultOf(j), j.Outcome ?? "", took);
    }

    // A record from before the harness kept its result is read off its outcome's words.
    static string ResultOf(JobRecord j)
    {
        if (j.Ended is null) return "running";
        if (j.Result is { } result) return result;
        var outcome = j.Outcome ?? "";
        if (outcome.StartsWith("failed: ", StringComparison.Ordinal)) return "broke";
        return outcome.StartsWith("red", StringComparison.Ordinal) || outcome.StartsWith("main red", StringComparison.Ordinal) ? "failed" : "ok";
    }

    internal static void Table(StringBuilder html, HarnessOptions o, IEnumerable<Row> rows, DateTimeOffset now)
    {
        html.Append("<table class=\"r jobs\"><tr><th>When</th><th class=role>Role</th><th>Work</th><th>Result</th><th class=out>Outcome</th><th class=\"n took\">Took</th><th></th></tr>");
        foreach (var r in rows)
        {
            var at = StatusPage.Local(r.Started);
            var when = at.Date == StatusPage.Local(now).Date ? $"{at:HH:mm}" : $"{at:dd'/'MM HH:mm}";
            var role = StatusPage.RoleOfWork(r.Work);
            var line = r.Outcome.Split('\n', 2)[0].Trim();
            html.Append($"<tr><td class=when>{when}</td><td class=role>{StatusPage.Swatch(role)}{StatusPage.Esc(StatusPage.Title(role))}</td>");
            html.Append($"<td>{StatusPage.Linked(o, r.Work)}</td><td>{Pill(r.Result)}</td>");
            html.Append($"<td class=out title=\"{StatusPage.Esc(r.Outcome)}\">{StatusPage.Esc(line)}</td><td class=\"n took\">{StatusPage.Esc(r.Took ?? "")}</td>");
            html.Append($"<td class=n><a class=icon href=\"/jobs/{Uri.EscapeDataString(r.Id)}\" aria-label=\"The job and its log\" title=\"The job and its log\">{Info}</a></td></tr>");
        }
        html.Append("</table>");
    }

    const string Info = "<svg width=16 height=16 viewBox=\"0 0 24 24\" fill=none stroke=currentColor stroke-width=2 stroke-linecap=round aria-hidden=true><circle cx=12 cy=12 r=9 /><path d=\"M12 11v5\" /><path d=\"M12 8h.01\" /></svg>";

    static string Pill(string result)
    {
        var (css, label) = result switch
        {
            "ok" => ("ok", "ok"),
            "failed" => ("warn", "failed"),
            "broke" => ("bad", "broke"),
            "quiet" => ("", "nothing to do"),
            "running" => ("acc", "running"),
            _ => ("", "—"),
        };
        return $"<span class=\"b {css}\">{label}</span>";
    }

    public static string List(HarnessOptions o, JobHistory history, string? role, string? result, int? story, int? page)
    {
        var now = DateTimeOffset.UtcNow;
        var all = history.Jobs().OrderByDescending(j => j.Started).Select(RowOf).ToList();
        var rows = all
            .Where(r => string.IsNullOrEmpty(role) || StatusPage.RoleOfWork(r.Work) == role)
            .Where(r => string.IsNullOrEmpty(result) || r.Result == result)
            .Where(r => story is null || r.Story == story)
            .ToList();
        var pages = Math.Max(1, (rows.Count + PageSize - 1) / PageSize);
        var at = Math.Clamp(page ?? 1, 1, pages);

        var html = Head("Jobs");
        html.Append($"<p class=muted style=\"font-size:13px\"><a href=\"/\">← Status</a></p><h1>Jobs <small class=muted>{StatusPage.Esc(o.Machine)} · the newest {all.Count} this machine ran</small></h1>");
        html.Append("<form class=filters method=get action=\"/jobs\">");
        html.Append(Select("role", "Every role", JobStats.Roles.Select(r => (r, StatusPage.Title(r))), role));
        html.Append(Select("result", "Every result", Results.Select(r => (r, r == "quiet" ? "nothing to do" : r)), result));
        html.Append($"<input name=story inputmode=numeric placeholder=\"Story #\" size=8 value=\"{story}\"><button>Filter</button>");
        if (!string.IsNullOrEmpty(role) || !string.IsNullOrEmpty(result) || story is not null) html.Append("<a href=\"/jobs\">Clear</a>");
        html.Append("</form>");

        if (rows.Count == 0)
        {
            html.Append("<p class=muted>No job matches.</p>");
            return html.ToString();
        }
        Table(html, o, rows.Skip((at - 1) * PageSize).Take(PageSize), now);
        html.Append("<div class=pager>");
        html.Append(at > 1 ? $"<a href=\"{Query(role, result, story, at - 1)}\">‹ Newer</a>" : "<span></span>");
        html.Append($"<span class=muted>page {at} of {pages} · {rows.Count} job{(rows.Count == 1 ? "" : "s")}</span>");
        html.Append(at < pages ? $"<a href=\"{Query(role, result, story, at + 1)}\">Older ›</a>" : "<span></span>");
        html.Append("</div>");
        return html.ToString();
    }

    public static string? One(HarnessOptions o, JobHistory history, string id, int? tail)
    {
        if (history.Jobs().LastOrDefault(j => j.Id == id) is not { } job) return null;
        var now = DateTimeOffset.UtcNow;
        var row = RowOf(job);
        var lines = Tails.Contains(tail ?? 200) ? tail ?? 200 : 200;
        var html = Head(job.Work, refresh: job.Ended is null);
        html.Append("<p class=muted style=\"font-size:13px\"><a href=\"/\">← Status</a> · <a href=\"/jobs\">All jobs</a></p>");
        var role = StatusPage.RoleOfWork(job.Work);
        html.Append($"<h1>{StatusPage.Linked(o, job.Work)}</h1>");
        html.Append("<table class=\"r meta\">");
        html.Append($"<tr><th>Result</th><td>{Pill(row.Result)}</td></tr>");
        html.Append($"<tr><th>Role</th><td>{StatusPage.Swatch(role)}{StatusPage.Esc(StatusPage.Title(role))}</td></tr>");
        if (job.Story is { } story) html.Append($"<tr><th>Story</th><td>{StatusPage.Linked(o, $"#{story}")} · <a href=\"/jobs?story={story}\">its jobs here</a></td></tr>");
        html.Append($"<tr><th>Started</th><td>{StatusPage.Local(job.Started):dd'/'MM HH:mm:ss}</td></tr>");
        html.Append($"<tr><th>{(job.Ended is null ? "Running for" : "Took")}</th><td>{StatusPage.Esc(row.Took ?? StatusPage.Time((now - job.Started).TotalSeconds))}{(job.Ended is { } e ? $" <span class=muted>(ended {StatusPage.Local(e):HH:mm:ss})</span>" : "")}</td></tr>");
        html.Append($"<tr><th>Id</th><td class=muted>{StatusPage.Esc(job.Id)}</td></tr>");
        html.Append("</table>");
        if (job.Outcome is { Length: > 0 } outcome) html.Append($"<h2>Outcome</h2><pre class=outcome>{StatusPage.Esc(outcome)}</pre>");

        html.Append("<div class=logbar><h2>Log</h2><span class=tails>");
        foreach (var t in Tails) html.Append($"<a href=\"/jobs/{Uri.EscapeDataString(id)}?tail={t}\"{(t == lines ? " class=on" : "")}>{(t == 0 ? "all" : $"last {t}")}</a>");
        html.Append($"<a href=\"/jobs/{Uri.EscapeDataString(id)}/log?tail={(lines == 0 ? 1000000 : lines)}\">raw</a></span></div>");
        html.Append(File.Exists(job.LogPath) ? $"<pre class=log>{StatusPage.Esc(Tail(job.LogPath, lines == 0 ? int.MaxValue : lines))}</pre>" : "<p class=muted>Its log is gone from this machine.</p>");
        return html.ToString();
    }

    internal static string Tail(string path, int lines)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return string.Join('\n', reader.ReadToEnd().Split('\n').TakeLast(lines));
    }

    static StringBuilder Head(string title, bool refresh = false)
    {
        var html = new StringBuilder();
        html.Append("<!doctype html><meta charset=utf-8><meta name=viewport content=\"width=device-width,initial-scale=1\">");
        if (refresh) html.Append("<meta http-equiv=refresh content=10>");
        html.Append($"<title>{StatusPage.Esc(title)}</title><style>{StatusPage.Style}</style>");
        return html;
    }

    static string Select(string name, string every, IEnumerable<(string Value, string Label)> options, string? chosen)
    {
        var html = new StringBuilder($"<select name={name}><option value=\"\">{every}</option>");
        foreach (var (value, label) in options) html.Append($"<option value=\"{value}\"{(value == chosen ? " selected" : "")}>{StatusPage.Esc(label)}</option>");
        return html.Append("</select>").ToString();
    }

    static string Query(string? role, string? result, int? story, int page)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(role)) parts.Add($"role={Uri.EscapeDataString(role)}");
        if (!string.IsNullOrEmpty(result)) parts.Add($"result={Uri.EscapeDataString(result)}");
        if (story is { } s) parts.Add($"story={s}");
        parts.Add($"page={page}");
        return "/jobs?" + string.Join("&amp;", parts);
    }
}
