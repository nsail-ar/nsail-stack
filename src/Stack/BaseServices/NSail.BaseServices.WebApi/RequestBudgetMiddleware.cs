// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Collections.Concurrent;
using System.Globalization;
using NSail.Data;
using NSail.Messaging.WebApi;
using NSail.Problems;
using NSail.Security;

namespace NSail.BaseServices.WebApi;

/// <summary>The tenant's request budget: how many messages its callers together may send per
/// minute, asked of <see cref="RequestBudgetProvider"/> and refused past it with 429 and a
/// <c>Retry-After</c> that says when the window reopens. The one ceiling that refuses
/// (data-tenancy.md): a rate defers the work rather than losing it, and one tenant's flood would
/// otherwise be spent out of every other tenant's share of the install.
///
/// <para>Only an authenticated request that sends a message counts. The matched route names the
/// message, as it does for <see cref="ThrottleMiddleware"/>, because a page load is dozens of
/// assets and none of them is work; and the caller is signed in, because a public door's
/// anonymous flood is the internet's and not the tenant's, and spending it out of the tenant's
/// budget would refuse the tenant's own users for it. A tenant whose provider answers null passes
/// untouched, and so does one it answers zero or less for: a figure that would refuse every
/// request is a suspension, and suspending is the state row's word
/// (<see cref="TenantStateMiddleware"/>), never a number's.</para>
///
/// <para>The window is counted here rather than by <c>System.Threading.RateLimiting</c>, whose
/// fixed window answers a refused lease's retry-after with the whole window and not what is left
/// of it — and the seconds left are the whole of what <c>Retry-After</c> is for.</para></summary>
public sealed class RequestBudgetMiddleware : IMiddleware
{
    static readonly TimeSpan Length = TimeSpan.FromMinutes(1);

    readonly TimeProvider _clock;
    readonly ConcurrentDictionary<string, Window> _windows = new(StringComparer.Ordinal);

    public RequestBudgetMiddleware(TimeProvider? clock = null)
    {
        _clock = clock ?? TimeProvider.System;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (await Budget(context) is { } budget
            && Spend(budget.Tenant, budget.PerMinute) is { } retryAfter)
        {
            await Refuse(context, retryAfter, budget.Message);

            return;
        }

        await next(context);
    }

    // Resolved per request: the provider and the tenant are the scope's, and this middleware is a
    // singleton because the counters are.
    static async Task<(string Tenant, int PerMinute, string Message)?> Budget(HttpContext context)
    {
        var message = context.GetEndpoint()?.Metadata.GetMetadata<MessageEndpointMetadata>()?.MessageType;
        var services = context.RequestServices;

        // Optional, as for CredentialMiddleware: a host that registered no SessionProvider has
        // nobody signed in, and nothing it serves is counted.
        if (message is null
            || services.GetService<SessionProvider>()?.Session is not { IsAuthenticated: true }
            || services.GetService<RequestBudgetProvider>() is not { } provider
            || await provider.PerMinute(context.RequestAborted) is not { } perMinute
            || perMinute < 1)
        {
            return null;
        }

        // The slug and not the row key: under MultiDb every tenant's row key is the install's
        // own, and a budget keyed on it would be one budget the whole install spends together.
        // A scope that resolved no tenant — every scope of an install under None, and of a host
        // with no data access — is the install's one customer.
        var tenancy = services.GetService<TenancyProvider>() ?? TenancyProvider.Install;

        return (tenancy.Current.Slug ?? string.Empty, perMinute, message.Name);
    }

    // The figure is read on every request rather than fixed when the window opened, so a plan
    // that changes takes effect on the next request.
    TimeSpan? Spend(string tenant, int perMinute)
    {
        var window = _windows.GetOrAdd(tenant, _ => new Window());
        var now = _clock.GetUtcNow();

        lock (window)
        {
            if (now - window.Opened >= Length)
            {
                window.Opened = now;
                window.Spent = 0;
            }

            if (window.Spent < perMinute)
            {
                window.Spent++;

                return null;
            }

            return window.Opened + Length - now;
        }
    }

    // Written here rather than thrown for the reason TenantStateMiddleware writes its own: the
    // error handler clears the response, and Retry-After is the half of this answer a client acts
    // on.
    static async Task Refuse(HttpContext context, TimeSpan retryAfter, string message)
    {
        var problem = SecurityProblem.TooManyRequests(message);

        context.Response.StatusCode = problem.Status!.Value;
        context.Response.Headers.RetryAfter =
            Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

        await context.Response.WriteAsJsonAsync(problem, cancellationToken: context.RequestAborted);
    }

    sealed class Window
    {
        public DateTimeOffset Opened { get; set; }

        public int Spent { get; set; }
    }
}
