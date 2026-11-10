// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using NSail.Harness.Api;
using NSail.Harness.Lanes;

namespace NSail.Harness;

// Posts this Harness's state to its Ops every so often, with the ended jobs it owes (OpsLedger),
// oldest first, as many as fit under Ops:MaxBytes; what is left goes with the next reports. Ops down
// or refusing costs one warning and the next report goes on time: nothing here may touch a pass.
public sealed class OpsReporter(IServiceScopeFactory scopes, HarnessOptions options, IHttpClientFactory clients, OpsLedger ledger, ILogger<OpsReporter> log) : BackgroundService
{
    const string Route = "api/ops/node/harness";

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        var ops = options.Ops;
        if (string.IsNullOrWhiteSpace(ops.Url)) return;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, ops.Minutes)));
        do
        {
            await Tick(Read, stop);
        }
        while (await timer.WaitForNextTickAsync(stop));
    }

    internal async Task Tick(Func<CancellationToken, Task<HarnessReport>> read, CancellationToken stop)
    {
        try
        {
            await Report(await read(stop), stop);
        }
        catch (Exception e) when (!stop.IsCancellationRequested)
        {
            log.LogWarning("the report to {Url} failed: {Why}", options.Ops.Url, e.Message);
        }
    }

    async Task<HarnessReport> Read(CancellationToken stop)
    {
        using var scope = scopes.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<HarnessHandler>();
        return new HarnessReport
        {
            Machine = options.Machine,
            Version = typeof(OpsReporter).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            Status = await handler.Handle(new GetStatus(), stop),
            Stats = await handler.Handle(new GetStats(), stop),
        };
    }

    async Task Report(HarnessReport report, CancellationToken stop)
    {
        var ops = options.Ops;
        var (body, sent) = Piece(report, ledger.Owed(), ops.MaxBytes);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{ops.Url!.TrimEnd('/')}/{Route}") { Content = new ByteArrayContent(body) };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ops.ResolveToken());
        using var answer = await clients.CreateClient(nameof(OpsReporter)).SendAsync(request, stop);
        answer.EnsureSuccessStatusCode();
        ledger.Accepted(sent);
    }

    // The report with the owed jobs that fit under the limit; a job too large to fit waits for a
    // lighter report rather than holding back the ones after it.
    static (byte[] Body, IReadOnlyList<JobRecord> Sent) Piece(HarnessReport report, IReadOnlyList<JobRecord> owed, int limit)
    {
        var size = With(report, []).Length;
        var sent = new List<JobRecord>();
        var jobs = new List<EndedJob>();
        foreach (var job in owed)
        {
            var ended = new EndedJob(job.Id, job.Work, job.Story, job.Started, job.Ended!.Value, job.Outcome, job.Result);
            var cost = JsonSerializer.SerializeToUtf8Bytes(ended, Json).Length + (jobs.Count > 0 ? 1 : 0);
            if (size + cost > limit) continue;
            size += cost;
            jobs.Add(ended);
            sent.Add(job);
        }
        return (With(report, jobs), sent);
    }

    static byte[] With(HarnessReport report, IReadOnlyList<EndedJob> jobs)
    {
        return JsonSerializer.SerializeToUtf8Bytes(new HarnessReport { Machine = report.Machine, Version = report.Version, Status = report.Status, Stats = report.Stats, Jobs = jobs }, Json);
    }

    static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;
}
