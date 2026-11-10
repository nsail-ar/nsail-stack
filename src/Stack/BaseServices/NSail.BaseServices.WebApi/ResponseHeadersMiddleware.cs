// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Net.Http.Headers;

namespace NSail.BaseServices.WebApi;

/// <summary>Sets the browser-hardening headers on every response this host gives:
/// <c>X-Content-Type-Options: nosniff</c>, a <c>Referrer-Policy</c> and a
/// <c>Permissions-Policy</c> that switches off the device features the Stack never uses. A header
/// an endpoint or the proxy already set is left as it is.</summary>
public sealed class ResponseHeadersMiddleware : IMiddleware
{
    const string ReferrerPolicy = "strict-origin-when-cross-origin";

    const string PermissionsPolicy = "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";

    static readonly Func<object, Task> s_stamp = Stamp;

    public Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        // A callback, because ErrorMiddleware clears the response it rewrites.
        context.Response.OnStarting(s_stamp, context);

        return next(context);
    }

    static Task Stamp(object state)
    {
        var headers = ((HttpContext)state).Response.Headers;

        headers.TryAdd(HeaderNames.XContentTypeOptions, "nosniff");
        headers.TryAdd("Referrer-Policy", ReferrerPolicy);
        headers.TryAdd("Permissions-Policy", PermissionsPolicy);

        return Task.CompletedTask;
    }
}
