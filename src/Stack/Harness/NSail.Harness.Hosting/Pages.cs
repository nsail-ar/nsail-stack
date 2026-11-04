// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Lanes;
using NSail.Harness.Planning;

namespace NSail.Harness.Hosting;

// What a person opens in a browser: the status page, the jobs (/jobs: filtered, a page at a time), one
// job and its log (/jobs/{id}), and a log as plain text (/jobs/{id}/log). The API is the
// Sdk's messages (/api/harness/*, /scalar).
public static class Pages
{
    public static void MapPages(this WebApplication app)
    {
        app.MapGet("/", (HarnessLoop loop, HarnessOptions o, JobHistory history, BuildLeases leases, JobStats stats, LoadSamples load, Agents.IAgent agent, string? range) =>
            Results.Content(StatusPage.Render(loop, o, history, leases, stats, load, agent, range), "text/html; charset=utf-8"))
            .ExcludeFromDescription();
        app.MapGet("/jobs", (HarnessOptions o, JobHistory history, string? role, string? result, int? story, int? page) =>
            Results.Content(JobsPage.List(o, history, role, result, story, page), "text/html; charset=utf-8"))
            .ExcludeFromDescription();
        app.MapGet("/jobs/{id}", (string id, HarnessOptions o, JobHistory history, int? tail) =>
            JobsPage.One(o, history, id, tail) is { } html ? Results.Content(html, "text/html; charset=utf-8") : Results.NotFound())
            .ExcludeFromDescription();
        app.MapGet("/jobs/{id}/log", (string id, JobHistory history, int? tail) =>
        {
            var job = history.Jobs().LastOrDefault(j => j.Id == id);
            if (job is null || !File.Exists(job.LogPath)) return Results.NotFound();
            return Results.Text(JobsPage.Tail(job.LogPath, tail ?? 200), "text/plain; charset=utf-8");
        });
    }
}
