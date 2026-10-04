// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSail.Data;
using NSail.Security;

namespace NSail.Background;

/// <summary>The single consumer of <see cref="DeferredWork"/>'s queue. One item at a time, on
/// its own scope: a second item never runs while the ambient tenant an <c>Enter</c> pinned for
/// the first is still live on this flow (data-tenancy.md, Background jobs), and the volumes this
/// exists for — a handful of notifications, never a bulk send — do not ask for more than one.
///
/// <para>A slow item costs this item's own budget and nothing past it: the timeout below is
/// what keeps a hung vendor from turning "fire and forget" into "forget forever" for
/// everybody queued behind it.</para></summary>
sealed class DeferredWorkRunner : BackgroundService
{
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    readonly Meter _meter = new(DeferredWorkMetrics.MeterName);
    readonly Counter<long> _completed;

    readonly IServiceScopeFactory _scopes;
    readonly DeferredWork _queue;
    readonly ILogger<DeferredWorkRunner> _logger;

    public DeferredWorkRunner(IServiceScopeFactory scopes, DeferredWork queue, ILogger<DeferredWorkRunner> logger)
    {
        _scopes = scopes;
        _queue = queue;
        _logger = logger;

        _completed = _meter.CreateCounter<long>(DeferredWorkMetrics.Completed, description: "Deferred work items handled, by kind and outcome.");
    }

    public override void Dispose()
    {
        _meter.Dispose();

        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in _queue.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            await Run(item, stoppingToken).ConfigureAwait(false);
        }
    }

    async Task Run(DeferredWorkItem item, CancellationToken stoppingToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

        budget.CancelAfter(Timeout);

        try
        {
            await using var scope = _scopes.CreateAsyncScope();

            // Exactly what a request edge and BackgroundJobRunner both do: a deferred item acts
            // for nobody of its own, and every id it needs it already carries as a value baked
            // into its closure.
            scope.ServiceProvider.GetRequiredService<SessionProvider>().Session = Session.System();

            if (item.Tenant.IsResolved)
            {
                scope.ServiceProvider.GetRequiredService<ResolvedTenancyProvider>().Enter(item.Tenant);
            }

            await item.Work(scope.ServiceProvider, budget.Token).ConfigureAwait(false);

            Report(item.Kind, DeferredWorkMetrics.Succeeded);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            // budget fired and not the host: the item ran past its own timeout, not past
            // shutdown.
            Report(item.Kind, DeferredWorkMetrics.TimedOut);

            _logger.LogError("Deferred work timed out after {Timeout}; the queue continues with the next item.", Timeout);
        }
        catch (Exception exception)
        {
            Report(item.Kind, DeferredWorkMetrics.Failed);

            _logger.LogError(exception, "Deferred work failed; the queue continues with the next item.");
        }
    }

    void Report(string kind, string outcome)
    {
        _completed.Add(
            1,
            new KeyValuePair<string, object?>(DeferredWorkMetrics.KindTag, kind),
            new KeyValuePair<string, object?>(DeferredWorkMetrics.OutcomeTag, outcome));
    }
}
