// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Diagnostics.Metrics;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSail.Configuration;
using NSail.Localization;

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
    readonly CultureInfo? _culture;

    // The configuration is optional for the harness that composes no host; a host always has
    // one, and it is where the install's language lives.
    public DeferredWorkRunner(
        IServiceScopeFactory scopes,
        DeferredWork queue,
        ILogger<DeferredWorkRunner> logger,
        IConfiguration? configuration = null)
    {
        _scopes = scopes;
        _queue = queue;
        _logger = logger;
        _culture = configuration is null ? null : CultureInfo.GetCultureInfo(configuration.Load<LanguageOptions>().Default);

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

        // Exactly what the request edge does (UseRequestLanguage) and BackgroundJobRunner does
        // per loop, from the other place that has no request: a deferred item runs in the
        // install's default language. Without it the queue runs invariant, LanguageProvider
        // seeds itself with "iv", and the WhatsApp channel refuses the send before the
        // transport because no account holds the template in a language nobody has — the order
        // confirmation that never reached a phone (nsail#1550), with the mail body rendered out
        // of the base catalog beside it. Per item rather than once per loop: the item's own work
        // may switch culture for whoever it writes to, and the next item is not its reader.
        if (_culture is { } culture)
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        try
        {
            await using var scope = _scopes.CreateAsyncScope();

            item.Enter(scope.ServiceProvider);

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
