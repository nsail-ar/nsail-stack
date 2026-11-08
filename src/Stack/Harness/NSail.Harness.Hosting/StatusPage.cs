// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using System.Net;
using System.Text;
using NSail.Harness.Agents;
using NSail.Harness.Api;
using NSail.Harness.Lanes;
using NSail.Harness.Planning;

namespace NSail.Harness.Hosting;

// The harness at a glance, refreshed every few seconds: where it stands, what the crew did over a
// range (JobStats, drawn here as plain SVG: no script, no library), what runs, what the last pass
// planned, what ended. Times are the machine's.
static class StatusPage
{
    // One colour per role, in the order JobStats.Roles names them.
    static readonly string[] Colors = ["#2a78d6", "#eb6834", "#1baf7a", "#eda100", "#e87ba4", "#6250d6", "#0e9aa7"];
    static readonly string[] Ranges = ["24h", "7d", "30d"];

    internal const string Style = """
        :root{--bg:#fff;--fg:#1f1f1e;--muted:#6b6a66;--faint:#9a9893;--card:#f5f4f0;--line:#e4e2dc;--ok:#2f7d14;--okbg:#e9f3df;--bad:#b3261e;--badbg:#fbe9e7;--warn:#8a5a00;--warnbg:#fbf0d9;--acc:#1f5fa8;--accbg:#e6f0fb}
        @media (prefers-color-scheme:dark){:root{--bg:#1a1a19;--fg:#ecebe6;--muted:#b4b2a9;--faint:#8a8881;--card:#252523;--line:#363633;--ok:#9fd57a;--okbg:#22331a;--bad:#f09a92;--badbg:#3d1f1c;--warn:#f0c46a;--warnbg:#3a2e14;--acc:#8bbcf0;--accbg:#1b2d42}}
        body{font:14px system-ui;margin:24px;max-width:1200px;background:var(--bg);color:var(--fg)}
        h1{font-size:20px;font-weight:600}h2{font-size:15px;font-weight:600;margin:22px 0 8px}
        td,th{padding:3px 10px;text-align:left;vertical-align:top}.muted{color:var(--muted)}a{color:var(--acc)}
        .badges{display:flex;flex-wrap:wrap;gap:6px;margin:8px 0 14px}.b{font-size:12px;padding:3px 10px;border-radius:6px;background:var(--card);color:var(--muted)}
        .b.ok{background:var(--okbg);color:var(--ok)}.b.bad{background:var(--badbg);color:var(--bad)}.b.warn{background:var(--warnbg);color:var(--warn)}.b.acc{background:var(--accbg);color:var(--acc)}
        .ranges a{font-size:12px;padding:3px 10px;border:1px solid var(--line);border-radius:6px;text-decoration:none;color:var(--muted);margin-left:4px}.ranges a.on{background:var(--card);color:var(--fg)}
        .kpis{display:grid;grid-template-columns:repeat(auto-fit,minmax(140px,1fr));gap:10px;margin-bottom:12px}.k{background:var(--card);border-radius:8px;padding:10px 14px}
        .k p{margin:0}.k .l{font-size:12px;color:var(--muted)}.k .v{font-size:22px;font-weight:600;margin:2px 0}.k .d{font-size:12px;color:var(--muted)}
        .legend{display:flex;flex-wrap:wrap;gap:12px;font-size:12px;color:var(--muted)}.sw{display:inline-block;width:10px;height:10px;border-radius:2px;margin-right:4px;vertical-align:-1px}
        .cols{display:grid;grid-template-columns:minmax(0,1.5fr) minmax(0,1fr);gap:28px}@media (max-width:760px){.cols{grid-template-columns:minmax(0,1fr)}body{margin:16px}}
        table.r{width:100%;border-collapse:collapse}table.r td,table.r th{border-bottom:1px solid var(--line);padding:5px 6px}table.r th{font-weight:600;font-size:12px;color:var(--muted)}table.r .n{text-align:right;white-space:nowrap}@media (max-width:520px){table.r{font-size:12px}table.r td,table.r th{padding:4px 3px}}
        .meter{height:6px;background:var(--card);border-radius:3px;margin:3px 0 10px}.meter div{height:6px;border-radius:3px;background:#2a78d6}
        table.jobs{font-size:13px}table.jobs td{vertical-align:middle}table.jobs .when,table.jobs .took{white-space:nowrap;color:var(--muted);font-variant-numeric:tabular-nums}
        table.jobs .role{white-space:nowrap;color:var(--muted);font-size:12px}table.jobs td.out{max-width:0;width:34%;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;color:var(--muted)}
        table.jobs .b{white-space:nowrap}.icon{display:inline-flex;padding:4px;border-radius:6px;color:var(--muted)}.icon:hover{background:var(--card);color:var(--fg)}
        @media (max-width:760px){table.jobs .role,table.jobs .out,table.jobs .took{display:none}}
        .filters{display:flex;flex-wrap:wrap;gap:8px;align-items:center;margin:4px 0 12px}.filters a{font-size:13px}
        select,input,button{font:inherit;font-size:13px;padding:4px 8px;border:1px solid var(--line);border-radius:6px;background:var(--bg);color:var(--fg)}button{cursor:pointer;background:var(--card)}
        .pager{display:flex;justify-content:space-between;align-items:center;margin:12px 0;font-size:13px}
        table.meta{width:auto;font-size:13px}table.meta th{width:110px}
        pre{font:12px/1.5 ui-monospace,Consolas,monospace;background:var(--card);border-radius:8px;padding:10px 12px;white-space:pre-wrap;overflow-wrap:anywhere}pre.log{max-height:70vh;overflow:auto}
        .logbar{display:flex;justify-content:space-between;align-items:baseline;flex-wrap:wrap;gap:8px}.tails a{font-size:12px;padding:3px 10px;border:1px solid var(--line);border-radius:6px;text-decoration:none;color:var(--muted);margin-left:4px}.tails a.on{background:var(--card);color:var(--fg)}
        .row{display:flex;justify-content:space-between;flex-wrap:wrap;gap:4px 12px;font-size:13px;margin:3px 0}
        """;

    public static string Render(HarnessLoop loop, HarnessOptions o, JobHistory history, BuildLeases leases, JobStats stats, LoadSamples load, IAgent agent, string? range)
    {
        var now = DateTimeOffset.UtcNow;
        var view = stats.View(Ranges.Contains(range) ? range! : "24h", now);
        var html = new StringBuilder();
        html.Append("<!doctype html><meta charset=utf-8><meta name=viewport content=\"width=device-width,initial-scale=1\"><meta http-equiv=refresh content=15><title>Harness</title>");
        html.Append($"<style>{Style}</style>");
        var version = view.Starts.LastOrDefault()?.Version;
        html.Append($"<h1>Harness <small class=muted>{Esc(o.DefaultRepository.Name)} · {Esc(o.Machine)} · {o.Mode}{(version is null ? "" : $" · {Esc(version)}")}</small></h1>");
        var running = loop.Running;
        Badges(html, loop, running.Count, history, leases, load, now);
        Stats(html, view);
        Sides(html, view, o, agent, now);

        Running(html, o, running, now);

        if (loop.Last is { } pass)
        {
            html.Append($"<p class=muted style=\"font-size:12px\">Last pass {Local(pass.At):HH:mm:ss}, {pass.Took.TotalSeconds:F1}s</p>");
            if (pass.Error is not null) html.Append($"<p style=\"color:var(--bad)\">The pass failed: {Esc(pass.Error)}</p>");
            if (pass.Plan is { } plan) PlanPage.Summary(html, o, plan);
        }

        var ended = history.Jobs().Where(j => j.Ended is not null).ToList();
        html.Append($"<div class=logbar><h2>Recent jobs</h2><a href=\"/jobs\" style=\"font-size:13px\">All {ended.Count} jobs, with filters ›</a></div>");
        JobsPage.Table(html, o, ended.OrderByDescending(j => j.Started).Take(10).Select(JobsPage.RowOf), now);
        return html.ToString();
    }

    internal static void Running(StringBuilder html, HarnessOptions o, IReadOnlyCollection<RunningJob> running, DateTimeOffset now)
    {
        html.Append($"<h2>Running ({running.Count})</h2>");
        if (running.Count == 0) html.Append("<p class=muted>Nothing runs now.</p>");
        else JobsPage.Table(html, o, running.OrderBy(r => r.Started).Select(r => new JobsPage.Row(r.Id, r.Started, r.Item.ToString(), r.Item.Story, "running", $"slot {r.Slot}", Time((now - r.Started).TotalSeconds), r)), now);
    }

    internal static string Swatch(string role)
    {
        var index = Array.IndexOf(JobStats.Roles, role);
        return $"<span class=sw style=\"background:{(index < 0 ? "var(--faint)" : Colors[index])}\"></span>";
    }

    // A job's role read off its work's first word (`gate PR #837…`, `dev #835 (…)`, `gate main at…`).
    internal static string RoleOfWork(string work)
    {
        return work.Split(' ', 2)[0] switch
        {
            "analyze" => "analyst",
            "dev" => "dev",
            "review" => "reviewer",
            "qa" => "qa",
            "release" => "release",
            "gate" => "gate",
            "watch" => "watch",
            _ => "",
        };
    }

    // A story's and a PR's number linked to the repository, where it is GitHub's (it sends an issue's
    // address on to the PR's).
    internal static string Linked(HarnessOptions o, string text)
    {
        var escaped = Esc(text);
        if (o.DefaultRepository.Provider != "GitHub") return escaped;
        // Another repository's PR is named by its short name (`nsail-stack PR #130`); any other #N is the
        // default repository's.
        return System.Text.RegularExpressions.Regex.Replace(escaped, @"(?:\b([\w.-]+) PR )?#(\d+)", m =>
        {
            var named = m.Groups[1].Success ? o.Repositories.FirstOrDefault(r => r.Provider == "GitHub" && r.Name.EndsWith($"/{m.Groups[1].Value}", StringComparison.Ordinal)) : null;
            var repo = named?.Name ?? o.DefaultRepository.Name;
            var lead = m.Groups[1].Success ? $"{m.Groups[1].Value} PR " : "";
            return $"{lead}<a href=\"https://github.com/{Esc(repo)}/issues/{m.Groups[2].Value}\">#{m.Groups[2].Value}</a>";
        });
    }

    static void Badges(StringBuilder html, HarnessLoop loop, int running, JobHistory history, BuildLeases leases, LoadSamples load, DateTimeOffset now)
    {
        html.Append("<div class=badges>");
        if (loop.Paused is { } why) html.Append($"<span class=\"b warn\">paused: {Esc(why)}</span>");
        if (loop.AgentsOffUntil is { } wall) html.Append($"<span class=\"b warn\">agents off: out of quota until {Local(wall):HH:mm}; builds go on</span>");
        html.Append($"<span class=\"b acc\">{running} running</span>");
        if (loop.Last is { Plan: { } plan } pass) html.Append($"<span class=b>{Math.Max(0, plan.Items.Count - pass.Started.Count)} waiting</span>");
        html.Append(loop.Stalled > 0 ? $"<span class=\"b warn\">{loop.Stalled} stalled</span>" : "<span class=b>0 stalled</span>");
        if (history.Jobs().LastOrDefault(j => j.Work.StartsWith("gate main", StringComparison.Ordinal) && j.Outcome is not null) is { } main)
        {
            var green = main.Outcome!.StartsWith("main green", StringComparison.Ordinal);
            html.Append($"<span class=\"b {(green ? "ok" : "bad")}\">{Esc(main.Outcome)}</span>");
        }
        var cpu = load.Cpu(now, TimeSpan.FromMinutes(1));
        html.Append($"<span class=b>CPU {(cpu is { } c ? $"{c:F0}%" : "—")} · builds {leases.Held}/{leases.Limit}{(leases.Waiting > 0 ? $", {leases.Waiting} waiting" : "")}</span>");
        html.Append("</div>");
    }

    internal static void Stats(StringBuilder html, HarnessStatsView view)
    {
        var span = view.Range switch { "7d" => "7 days", "30d" => "30 days", _ => "24 h" };
        html.Append("<div style=\"display:flex;justify-content:space-between;align-items:baseline;flex-wrap:wrap;gap:8px\"><h2>The crew, last ").Append(span).Append("</h2><span class=ranges>");
        foreach (var r in Ranges) html.Append($"<a href=\"/?range={r}\"{(r == view.Range ? " class=on" : "")}>{r.Replace("h", " h").Replace("d", " d")}</a>");
        html.Append("</span></div>");

        var t = view.Totals;
        var delta = t.Jobs - t.Previous;
        html.Append("<div class=kpis>");
        Kpi(html, "Jobs", t.Jobs.ToString(CultureInfo.InvariantCulture), $"{(delta >= 0 ? "+" : "")}{delta} vs the {span} before");
        Kpi(html, "Success rate", t.Jobs == 0 ? "—" : $"{100.0 * t.Ok / t.Jobs:F0}%", $"{t.Ok} of {t.Jobs}");
        Kpi(html, "PRs merged", t.Merged.ToString(CultureInfo.InvariantCulture), "landed by the reviewer");
        Kpi(html, "Story lead time", t.LeadTime is { } lead ? Time(lead) : "—", "first job to merged, median");
        html.Append("</div>");

        html.Append("<div class=legend>");
        for (var i = 0; i < JobStats.Roles.Length; i++) html.Append($"<span><span class=sw style=\"background:{Colors[i]}\"></span>{Esc(Title(JobStats.Roles[i]))}</span>");
        html.Append("<span><span class=sw style=\"background:var(--faint);width:2px\"></span>harness started</span></div>");
        Chart(html, view);
    }

    static void Kpi(StringBuilder html, string label, string value, string detail)
    {
        html.Append($"<div class=k><p class=l>{Esc(label)}</p><p class=v>{Esc(value)}</p><p class=d>{Esc(detail)}</p></div>");
    }

    // Jobs per bucket, stacked by role; a dashed line where the harness started.
    static void Chart(StringBuilder html, HarnessStatsView view)
    {
        const double W = 1000, H = 190, left = 34, top = 16, bottom = 160;
        var n = view.Buckets.Count;
        var tallest = view.Buckets.Select(b => b.Jobs.Sum(j => j.Count)).DefaultIfEmpty(0).Max();
        var max = Math.Max(4, (int)Math.Ceiling(tallest / 4.0) * 4);
        var width = (W - left - 6) / Math.Max(1, n);
        var hourly = view.Range == "24h";
        html.Append($"<svg viewBox=\"0 0 {W} {H}\" width=\"100%\" role=img aria-label=\"Jobs per {(hourly ? "hour" : "day")} by role\">");
        for (var g = 0; g <= max; g += max / 4)
        {
            var y = bottom - (bottom - top) * g / max;
            html.Append(Svg($"<line x1={left} x2={W - 6} y1={y:F1} y2={y:F1} stroke=\"var(--line)\" stroke-width=1 /><text x={left - 6} y={y + 4:F1} text-anchor=end font-size=11 fill=\"var(--faint)\">{g}</text>"));
        }
        var every = hourly ? 3 : Math.Max(1, n / 7);
        for (var i = 0; i < n; i++)
        {
            var b = view.Buckets[i];
            var x = left + i * width + 2;
            var y = bottom;
            var at = Local(b.At);
            var when = hourly ? $"{at:HH}:00" : $"{at:ddd dd'/'MM}";
            for (var r = 0; r < b.Jobs.Count; r++)
            {
                if (b.Jobs[r].Count == 0) continue;
                var h = (bottom - top) * b.Jobs[r].Count / max;
                y -= h;
                html.Append(Svg($"<rect x={x:F1} y={y + 1:F1} width={Math.Max(1, width - 4):F1} height={Math.Max(1, h - 2):F1} rx=2 fill=\"{Colors[r]}\"><title>{when} · {Title(b.Jobs[r].Role)}: {b.Jobs[r].Count}</title></rect>"));
            }
            if (i % every == 0) html.Append(Svg($"<text x={x + (width - 4) / 2:F1} y={bottom + 16} text-anchor=middle font-size=11 fill=\"var(--faint)\">{(hourly ? $"{at:HH}h" : $"{at:dd'/'MM}")}</text>"));
        }
        var length = (view.To - view.From).TotalSeconds;
        foreach (var s in view.Starts)
        {
            var x = left + (W - left - 6) * (s.At - view.From).TotalSeconds / length;
            html.Append(Svg($"<line x1={x:F1} x2={x:F1} y1={top - 6} y2={bottom} stroke=\"var(--faint)\" stroke-dasharray=\"3 3\" stroke-width=1><title>started {Local(s.At):dd'/'MM HH:mm}{(s.Version is null ? "" : $" on {Esc(s.Version)}")}</title></line>"));
        }
        html.Append("</svg>");
    }

    // A role a machine may never run (no QA walker, no release, no watch) shows only once it has jobs.
    internal static void RoleTable(StringBuilder html, HarnessStatsView view)
    {
        html.Append("<h2>By role</h2><table class=r><tr><th>Role</th><th class=n>Jobs</th><th class=n>OK</th><th class=n>Failed</th><th class=n>Broke</th><th class=n>p50</th><th class=n>p95</th><th class=n>Wait</th></tr>");
        for (var i = 0; i < view.Roles.Count; i++)
        {
            var r = view.Roles[i];
            if (r.Jobs == 0 && r.Role is "qa" or "release" or "watch") continue;
            html.Append($"<tr><td><span class=sw style=\"background:{Colors[i]}\"></span>{Esc(Title(r.Role))}</td><td class=n>{r.Jobs}</td><td class=n>{(r.Jobs == 0 ? "—" : $"{100.0 * r.Ok / r.Jobs:F0}%")}</td><td class=n>{r.Failed}</td><td class=n>{r.Broke}</td><td class=n>{Time(r.P50)}</td><td class=n>{Time(r.P95)}</td><td class=n>{Time(r.Wait)}</td></tr>");
        }
        html.Append("</table><p class=muted style=\"font-size:12px\">Failed: its work went wrong (a red gate, a dead voyage). Broke: the harness's own break. Wait: from first planned to started. Times are a histogram's bucket edges.</p>");
    }

    static void Sides(StringBuilder html, HarnessStatsView view, HarnessOptions o, IAgent agent, DateTimeOffset now)
    {
        html.Append("<div class=cols><div>");
        RoleTable(html, view);
        html.Append("</div><div>");

        html.Append("<h2>Agent quota</h2>");
        var seats = o.Seats.Count == 0 ? "" : $" <span class=muted>· {Esc(string.Join(", ", o.Seats.Keys))}</span>";
        html.Append($"<p style=\"margin:0 0 4px\"><b>{Esc(Title(agent.Driver))}</b>{seats}</p>");
        if (agent.Usage.Count == 0)
        {
            html.Append("<p class=muted>No run has reported its quota yet.</p>");
        }
        foreach (var w in agent.Usage)
        {
            var used = Math.Clamp(w.Utilization * 100, 0, 100);
            var reset = Local(w.ResetsAt);
            var when = w.ResetsAt - now < TimeSpan.FromHours(20) ? $"{reset:HH:mm}" : $"{reset:ddd HH:mm}";
            html.Append($"<div class=row><span class=muted>{Esc(w.Name)}</span><span class=muted>{used:F0}%, resets {when}</span></div><div class=meter><div style=\"width:{used.ToString("F0", CultureInfo.InvariantCulture)}%\"></div></div>");
        }

        var t = view.Totals;
        html.Append("<h2>Rework</h2>");
        html.Append($"<div class=row><span>Dev rounds per story</span><span>{(t.DevRoundsPerStory is { } rounds ? rounds.ToString("F1", CultureInfo.InvariantCulture) : "—")}</span></div>");
        html.Append($"<div class=row><span>Reviews with findings</span><span>{(t.Reviews == 0 ? "—" : $"{100.0 * t.ReviewsWithFindings / t.Reviews:F0}% ({t.ReviewsWithFindings} of {t.Reviews})")}</span></div>");
        html.Append("</div></div>");
    }

    // A duration as a person reads it: 45 s, 12 m, 3 h 40 m.
    internal static string Time(double? seconds)
    {
        if (seconds is not { } s) return "—";
        if (s < 60) return $"{s:F0} s";
        if (s < 3600) return $"{s / 60:F0} m";
        var span = TimeSpan.FromSeconds(s);
        return span.Minutes == 0 ? $"{(int)span.TotalHours} h" : $"{(int)span.TotalHours} h {span.Minutes} m";
    }

    internal static string Title(string role)
    {
        return role switch { "qa" => "QA", "" => "", _ => char.ToUpperInvariant(role[0]) + role[1..] };
    }

    internal static DateTimeOffset Local(DateTimeOffset at)
    {
        return TimeZoneInfo.ConvertTime(at, TimeZoneInfo.Local);
    }

    // SVG attributes written with the invariant culture: a machine that writes 1,5 breaks every coordinate.
    static string Svg(FormattableString markup)
    {
        return markup.ToString(CultureInfo.InvariantCulture);
    }

    internal static string Esc(string s)
    {
        return WebUtility.HtmlEncode(s);
    }
}
