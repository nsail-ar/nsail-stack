// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using NSail.Harness.Api;

namespace NSail.Harness;

// Posts this Harness's state to its Ops every so often. Ops down or refusing costs one warning and
// the next report goes on time: nothing here may touch a pass.
public sealed class OpsReporter(IServiceScopeFactory scopes, HarnessOptions options, IHttpClientFactory clients, ILogger<OpsReporter> log) : BackgroundService
{
    const string Route = "api/ops/node/harness";

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        var ops = options.Ops;
        if (string.IsNullOrWhiteSpace(ops.Url)) return;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, ops.Minutes)));
        do
        {
            try
            {
                await Report(ops, stop);
            }
            catch (Exception e) when (!stop.IsCancellationRequested)
            {
                log.LogWarning("the report to {Url} failed: {Why}", ops.Url, e.Message);
            }
        }
        while (await timer.WaitForNextTickAsync(stop));
    }

    async Task Report(OpsOptions ops, CancellationToken stop)
    {
        using var scope = scopes.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<HarnessHandler>();
        var report = new HarnessReport
        {
            Machine = options.Machine,
            Version = typeof(OpsReporter).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            Status = await handler.Handle(new GetStatus(), stop),
            Stats = await handler.Handle(new GetStats(), stop),
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{ops.Url!.TrimEnd('/')}/{Route}") { Content = JsonContent.Create(report) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ops.ResolveToken());
        using var answer = await clients.CreateClient(nameof(OpsReporter)).SendAsync(request, stop);
        answer.EnsureSuccessStatusCode();
    }
}
