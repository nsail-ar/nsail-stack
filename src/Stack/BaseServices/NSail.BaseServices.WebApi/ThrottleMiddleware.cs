// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
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

    const int MaxIdentityBodyBytes = 16 * 1024;

    const string IdentityItem = "nsail:throttle-identity";

    readonly PartitionedRateLimiter<HttpContext> _limiter;

    public ThrottleMiddleware()
    {
        _limiter = PartitionedRateLimiter.CreateChained(
            PartitionedRateLimiter.Create<HttpContext, string>(Partition),
            PartitionedRateLimiter.Create<HttpContext, string>(IdentityPartition));
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (Declared(context) is { } message)
        {
            if (Declaration(message) is { Identity: { } property })
            {
                context.Items[IdentityItem] = await Identity(context, property);
            }

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

    // Per message AND per submitted identity, from every address together. A send that names
    // none (the bind would refuse it anyway) is left to the per-address ceiling.
    static RateLimitPartition<string> IdentityPartition(HttpContext context)
    {
        if (Declared(context) is not { } message
            || Declaration(message) is not { Identity: not null } throttle
            || context.Items[IdentityItem] is not string identity
            || identity.Length == 0)
        {
            return RateLimitPartition.GetNoLimiter(string.Empty);
        }

        return RateLimitPartition.GetFixedWindowLimiter(
            $"{message.FullName}|identity|{identity}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = throttle.IdentityPerMinute > 0 ? throttle.IdentityPerMinute : throttle.PerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            });
    }

    // The identity is read before the body is bound, from the query or a JSON body, and the
    // body is rewound for the binder. Case and surrounding space are not a new account.
    static async Task<string?> Identity(HttpContext context, string property)
    {
        string? value = null;

        foreach (var pair in context.Request.Query)
        {
            if (string.Equals(pair.Key, property, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Value.ToString();
            }
        }

        if (value is null
            && context.Request.ContentLength is null or (> 0 and <= MaxIdentityBodyBytes)
            && context.Request.HasJsonContentType())
        {
            context.Request.EnableBuffering();

            try
            {
                // Chunked bodies state no length, so the read is bounded here: a body past the
                // bound is cut, does not parse, and names no identity.
                var buffer = new byte[MaxIdentityBodyBytes];
                var read = 0;
                int chunk;

                while (read < buffer.Length
                    && (chunk = await context.Request.Body.ReadAsync(buffer.AsMemory(read), context.RequestAborted)) > 0)
                {
                    read += chunk;
                }

                using var document = JsonDocument.Parse(buffer.AsMemory(0, read));

                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var member in document.RootElement.EnumerateObject())
                    {
                        if (member.Value.ValueKind == JsonValueKind.String
                            && string.Equals(member.Name, property, StringComparison.OrdinalIgnoreCase))
                        {
                            value = member.Value.GetString();
                        }
                    }
                }
            }
            catch (JsonException)
            {
            }
            finally
            {
                context.Request.Body.Position = 0;
            }
        }

        return value?.Trim().ToUpperInvariant();
    }

    // The connection's address, which UseForwardedHeaders has already rewritten to the real
    // client where the install states it runs behind a proxy. An IPv6 client owns a whole /64,
    // so the network is the caller: keyed by the full address it would rotate to a fresh budget
    // on every request. A request with no address at all — an in-memory test server — is one
    // caller rather than none: a partition key that varied would hand every request a budget of
    // its own and cap nothing.
    static string Caller(HttpContext context)
    {
        if (context.Connection.RemoteIpAddress is not { } address)
        {
            return "unknown";
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        Span<byte> bytes = stackalloc byte[16];

        address.TryWriteBytes(bytes, out _);
        bytes[8..].Clear();

        return new IPAddress(bytes) + "/64";
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
