// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Buffers;
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
/// caller and per message, and — where the message names an identity — a second fixed window per
/// message and submitted identity over every address together; what is not .NET's is the refusal, which speaks
/// <see cref="Problem"/> like every other door in this pipeline rather than a bare 429 the
/// client would have to recognise by its status alone.</para></summary>
public sealed class ThrottleMiddleware : IMiddleware, IDisposable
{
    // The attribute is metadata on a concrete type, so reading it is AOT-safe; the cache is
    // here because the lookup would otherwise run on every request of every endpoint.
    static readonly ConcurrentDictionary<Type, ThrottledAttribute?> Declarations = new();

    const int MaxIdentityBodyBytes = 16 * 1024;

    const int MaxIdentityChars = 256;

    // The partition of a send that declares an identity and yields none: a body past the bound,
    // not JSON, or without the property. One shared budget, so padding or omitting the field is
    // not a way out of the per-account ceiling.
    const string Unread = "\0unread";

    readonly PartitionedRateLimiter<HttpContext> _addressLimiter;

    readonly PartitionedRateLimiter<(Type Message, string Identity)> _identityLimiter;

    public ThrottleMiddleware()
    {
        _addressLimiter = PartitionedRateLimiter.Create<HttpContext, string>(Partition);
        _identityLimiter = PartitionedRateLimiter.Create<(Type Message, string Identity), string>(IdentityPartition);
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (Declared(context) is { } message)
        {
            using (var lease = await _addressLimiter.AcquireAsync(context, 1, context.RequestAborted))
            {
                if (!lease.IsAcquired)
                {
                    throw new BusinessException(SecurityProblem.TooManyRequests(message.Name));
                }
            }

            // After the address ceiling, so a send it refuses never pays for reading the body.
            if (Declaration(message) is { Identity: { } property })
            {
                var identity = await Identity(context, property) ?? Unread;

                using var lease = await _identityLimiter.AcquireAsync((message, identity), 1, context.RequestAborted);

                if (!lease.IsAcquired)
                {
                    throw new BusinessException(SecurityProblem.TooManyRequests(message.Name));
                }
            }
        }

        await next(context);
    }

    public void Dispose()
    {
        _addressLimiter.Dispose();
        _identityLimiter.Dispose();
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

    // Per message AND per submitted identity, from every address together.
    static RateLimitPartition<string> IdentityPartition((Type Message, string Identity) send)
    {
        var throttle = Declaration(send.Message)!;

        return RateLimitPartition.GetFixedWindowLimiter(
            $"{send.Message.FullName}|identity|{send.Identity}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = throttle.IdentityPerMinute > 0 ? throttle.IdentityPerMinute : throttle.PerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            });
    }

    // The identity is read before the body is bound, from the JSON body: the generator refuses
    // (NSG016) an Identity that is not a body member, so the binder reads the same place. A send
    // that is not JSON yields null, which the caller counts as Unread. Case and surrounding
    // space are not a new account.
    static async Task<string?> Identity(HttpContext context, string property)
    {
        if (!context.Request.HasJsonContentType())
        {
            return null;
        }

        var value = (await FromBody(context, property))?.Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return value.Length > MaxIdentityChars ? value[..MaxIdentityChars] : value;
    }

    static async Task<string?> FromBody(HttpContext context, string property)
    {
        if (context.Request.ContentLength > MaxIdentityBodyBytes)
        {
            return null;
        }

        // The buffer is memory only and capped at the bound: a longer body fails the read here
        // and again in the binder, and is never spilled to disk.
        context.Request.EnableBuffering(MaxIdentityBodyBytes, MaxIdentityBodyBytes);

        var buffer = ArrayPool<byte>.Shared.Rent(MaxIdentityBodyBytes);

        try
        {
            var read = 0;
            int chunk;

            while (read < MaxIdentityBodyBytes
                && (chunk = await context.Request.Body.ReadAsync(buffer.AsMemory(read, MaxIdentityBodyBytes - read), context.RequestAborted)) > 0)
            {
                read += chunk;
            }

            if (read == MaxIdentityBodyBytes
                && await context.Request.Body.ReadAsync(buffer.AsMemory(0, 1), context.RequestAborted) > 0)
            {
                return null;
            }

            using var document = JsonDocument.Parse(buffer.AsMemory(0, read));

            string? found = null;

            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                // The last occurrence wins, as it does when the binder assigns the members in
                // order: a repeated name must not let the sender pick a partition apart from
                // the value the handler receives.
                foreach (var member in document.RootElement.EnumerateObject())
                {
                    if (string.Equals(member.Name, property, StringComparison.OrdinalIgnoreCase))
                    {
                        found = member.Value.ValueKind == JsonValueKind.String ? member.Value.GetString() : null;
                    }
                }
            }

            return found;
        }
        catch (Exception exception) when (exception is JsonException or IOException or InvalidDataException)
        {
            return null;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);

            if (context.Request.Body.CanSeek)
            {
                context.Request.Body.Position = 0;
            }
        }
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
