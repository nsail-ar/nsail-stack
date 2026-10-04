// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.RateLimiting;
using NSail.Messaging.Annotations;
using NSail.Messaging.WebApi;
using NSail.Problems;

namespace NSail.BaseServices.WebApi;

/// <summary>The ceiling on a public door, read off the message the route already names. It
/// reaches for <see cref="MessageEndpointMetadata"/> exactly as
/// <see cref="EndpointGateMiddleware"/> does — the endpoint says which message it sends before
/// a byte of the request is bound, so nothing here is selected by a path literal that could
/// drift from the route the generator emitted.
///
/// <para>It stands in FRONT of the gate: volume is not a permission question, and refusing a
/// flood before a policy evaluation is the whole point of refusing it. An endpoint whose
/// message declares no <see cref="ThrottledAttribute"/> passes through untouched, so a host
/// composing no public door pays one metadata lookup per request and nothing else.</para>
///
/// <para>The limiter is .NET's own (<c>System.Threading.RateLimiting</c>), partitioned per
/// caller and per message; what is not .NET's is the refusal, which speaks
/// <see cref="Problem"/> like every other door in this pipeline rather than a bare 429 the
/// client would have to recognise by its status alone.</para></summary>
public sealed class ThrottleMiddleware : IMiddleware, IDisposable
{
    // The attribute is metadata on a concrete type, so reading it is AOT-safe; the cache is
    // here because the lookup would otherwise run on every request of every endpoint.
    static readonly ConcurrentDictionary<Type, ThrottledAttribute?> Declarations = new();

    readonly PartitionedRateLimiter<HttpContext> _limiter;

    public ThrottleMiddleware()
    {
        _limiter = PartitionedRateLimiter.Create<HttpContext, string>(Partition);
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (Declared(context) is { } message)
        {
            using var lease = await _limiter.AcquireAsync(context, 1, context.RequestAborted);

            if (!lease.IsAcquired)
            {
                throw new BusinessException(SecurityProblem.TooManyRequests(message.Name));
            }
        }

        await next(context);
    }

    public void Dispose()
    {
        _limiter.Dispose();
    }

    static RateLimitPartition<string> Partition(HttpContext context)
    {
        if (Declared(context) is not { } message || Declaration(message) is not { } throttle)
        {
            return RateLimitPartition.GetNoLimiter(string.Empty);
        }

        // Per message AND per caller: one budget shared across every public door would let a
        // flood on one of them close the others, and a budget per door alone would be a budget
        // the whole internet spends together.
        return RateLimitPartition.GetFixedWindowLimiter(
            $"{message.FullName}|{Caller(context)}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = throttle.PerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            });
    }

    // The connection's address, which UseForwardedHeaders has already rewritten to the real
    // client where the install states it runs behind a proxy. A request with no address at all
    // — an in-memory test server — is one caller rather than none: a partition key that varied
    // would hand every request a budget of its own and cap nothing.
    static string Caller(HttpContext context)
    {
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    static Type? Declared(HttpContext context)
    {
        var message = context.GetEndpoint()?.Metadata.GetMetadata<MessageEndpointMetadata>()?.MessageType;

        return message is not null && Declaration(message) is not null ? message : null;
    }

    static ThrottledAttribute? Declaration(Type message)
    {
        return Declarations.GetOrAdd(message, type => type.GetCustomAttribute<ThrottledAttribute>());
    }
}
