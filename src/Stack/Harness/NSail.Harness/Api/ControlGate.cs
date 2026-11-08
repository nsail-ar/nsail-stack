// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace NSail.Harness.Api;

// The door of the harness API. Loopback is no boundary: a seat's process reaches it. A request whose
// Host is not one the harness listens on is refused; the routes that drive the harness or serve a
// job's transcript need the control token, the build-turn routes the lease token (or the control
// one); the reads the status pages and the Ops report use stay open.
public sealed class ControlGate(RequestDelegate next, HarnessOptions options)
{
    public const string Header = "X-Harness-Token";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!HostAllowed(context.Request.Host.Host))
        {
            context.Response.StatusCode = StatusCodes.Status421MisdirectedRequest;
            return;
        }
        var need = Need(context.Request.Method, context.Request.Path);
        if (need != Access.Open)
        {
            var given = Given(context.Request);
            var control = options.Control.ResolveToken();
            var ok = control is not null && Same(given, control)
                || need == Access.Lease && Same(given, options.Control.ResolveLeaseToken());
            if (!ok)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
        }
        await next(context);
    }

    internal enum Access { Open, Lease, Control }

    internal static Access Need(string method, PathString path)
    {
        var p = path.Value?.TrimEnd('/').ToLowerInvariant() ?? "";
        if (p == "/api/harness/leases" && HttpMethods.IsPost(method)) return Access.Lease;
        if (p.StartsWith("/api/harness/leases/", StringComparison.Ordinal) && HttpMethods.IsDelete(method)) return Access.Lease;
        if (p.StartsWith("/api/harness/", StringComparison.Ordinal))
        {
            var read = HttpMethods.IsGet(method) || HttpMethods.IsHead(method);
            return read && p is "/api/harness/status" or "/api/harness/jobs" or "/api/harness/stats" ? Access.Open : Access.Control;
        }
        if (p.StartsWith("/jobs/", StringComparison.Ordinal)) return Access.Control;
        return Access.Open;
    }

    bool HostAllowed(string host)
    {
        if (host is "localhost" or "127.0.0.1" or "[::1]" or "::1") return true;
        if (Uri.TryCreate(options.PublicUrl, UriKind.Absolute, out var u) && string.Equals(u.Host, host, StringComparison.OrdinalIgnoreCase)) return true;
        return options.Control.Hosts.Any(h => string.Equals(h, host, StringComparison.OrdinalIgnoreCase));
    }

    static string? Given(HttpRequest request)
    {
        var auth = request.Headers.Authorization.ToString();
        if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return auth[7..].Trim();
        return request.Headers[Header].FirstOrDefault()?.Trim();
    }

    static bool Same(string? given, string expected)
    {
        return given is not null && CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(given)), SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
    }
}
