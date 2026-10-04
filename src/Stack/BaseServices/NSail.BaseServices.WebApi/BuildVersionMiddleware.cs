// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Builds;
using NSail.Messaging.Runtime.Context;

namespace NSail.BaseServices.WebApi;

/// <summary>Puts the build that answered on every response this host gives, under
/// <see cref="WireHeaders.Version"/>. It is the whole server half of a deploy reaching a client
/// that is already open: no endpoint to call, nothing the client has to remember to ask for,
/// and no polling — the client compares what it booted with against what came back.</summary>
public sealed class BuildVersionMiddleware : IMiddleware
{
    static readonly Func<object, Task> s_stamp = Stamp;

    public Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        // A callback rather than a header written on the way in: ErrorMiddleware clears the
        // response before it writes its Problem, and a refusal is exactly the answer a client
        // running last week's build is likeliest to get.
        context.Response.OnStarting(s_stamp, context);

        return next(context);
    }

    static Task Stamp(object state)
    {
        var context = (HttpContext)state;

        context.Response.Headers[WireHeaders.Version] = BuildVersion.Current;

        return Task.CompletedTask;
    }
}
