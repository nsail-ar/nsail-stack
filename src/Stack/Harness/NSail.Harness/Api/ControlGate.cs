// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using NSail.Messaging.WebApi;

namespace NSail.Harness.Api;

// The door of the harness API. Loopback is no boundary: a seat's process reaches it. A request whose
// Host is not one the harness listens on is refused; a message route needs the control token unless its
// message says otherwise (HarnessAccess: the build-turn routes take the lease token, the status reads are
// open); a page marked ControlOnly (a job's transcript) needs it too. A browser carries the token once
// as ?token= on a GET and keeps it in a cookie.
public sealed class ControlGate(RequestDelegate next, HarnessOptions options)
{
    public const string Header = "X-Harness-Token";
    public const string Cookie = "harness_token";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!HostAllowed(context.Request.Host))
        {
            context.Response.StatusCode = StatusCodes.Status421MisdirectedRequest;
            return;
        }
        var need = Need(context.GetEndpoint());
        if (need != HarnessAccess.Open)
        {
            var control = options.Control.ResolveToken();
            var browser = HttpMethods.IsGet(context.Request.Method);
            var given = Given(context.Request, browser);
            var ok = control is not null && Same(given.Token, control)
                || need == HarnessAccess.Lease && Same(given.Token, options.Control.ResolveLeaseToken());
            if (!ok)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
            if (given.FromQuery)
            {
                context.Response.Cookies.Append(Cookie, given.Token!, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Strict, Path = "/" });
            }
        }
        await next(context);
    }

    internal static HarnessAccess Need(Endpoint? endpoint)
    {
        if (endpoint?.Metadata.GetMetadata<MessageEndpointMetadata>() is { } message)
        {
            return message.MessageType.GetCustomAttribute<HarnessAccessAttribute>()?.Access ?? HarnessAccess.Control;
        }
        return endpoint?.Metadata.GetMetadata<ControlOnlyAttribute>() is not null ? HarnessAccess.Control : HarnessAccess.Open;
    }

    bool HostAllowed(HostString host)
    {
        var name = host.Host;
        if (name is "localhost" or "127.0.0.1" or "[::1]" or "::1") return true;
        if (Uri.TryCreate(options.PublicUrl, UriKind.Absolute, out var u) && string.Equals(u.Host, name, StringComparison.OrdinalIgnoreCase)) return true;
        return options.Control.Hosts.Any(h => string.Equals(h, name, StringComparison.OrdinalIgnoreCase));
    }

    static (string? Token, bool FromQuery) Given(HttpRequest request, bool browser)
    {
        var auth = request.Headers.Authorization.ToString();
        if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return (auth[7..].Trim(), false);
        if (request.Headers[Header].FirstOrDefault() is { Length: > 0 } header) return (header.Trim(), false);
        if (!browser) return (null, false);
        if (request.Query["token"].FirstOrDefault() is { Length: > 0 } query) return (query, true);
        return (request.Cookies[Cookie], false);
    }

    static bool Same(string? given, string expected)
    {
        return given is not null && CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(given)), SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
    }
}

// Marks a page's endpoint as one that needs the control token.
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class ControlOnlyAttribute : Attribute
{
}
