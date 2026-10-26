// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging.WebApi;
using NSail.Problems;
using NSail.Security;

namespace NSail.BaseServices.WebApi;

/// <summary>The gate, ahead of model binding. It does not replace SecurityInterceptor — that
/// one alone sees the built message and its field constraints, and stays the real guard — it
/// exists because the interceptor lives inside SendPipeline, and the pipeline only starts once
/// ASP.NET has bound every parameter: a required body or a required query member the caller
/// omitted is refused by the framework first, on behalf of someone with no permission to be
/// asked. Structural placement is the whole point, as it is for the two validation kinds
/// inside the pipeline (messaging.md).</summary>
public sealed class EndpointGateMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        await Guard(context);

        await next(context);
    }

    static async Task Guard(HttpContext context)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<MessageEndpointMetadata>() is not { } message)
        {
            return;
        }

        // Resolved per request rather than injected: the gate is as opt-in here as it is in
        // the pipeline, and a host that never called AddSecurityEnforcement — a client, a
        // Wasm host — registers neither the marker nor, necessarily, the manager at all.
        if (context.RequestServices.GetService<SecurityEnforcement>() is null)
        {
            return;
        }

        var security = context.RequestServices.GetRequiredService<SecurityManager>();
        var sessions = context.RequestServices.GetRequiredService<SessionProvider>();
        var session = sessions.Session;

        // Ahead of the policy half of PreRefusal below: a tenant's very first message since the
        // process started otherwise reads an empty, not-yet-loaded policy set here and
        // denies before Authorize ever gets a chance at the real one.
        await security.EnsureLoaded(context.RequestAborted).ConfigureAwait(false);

        // PreRefusal, not Authorize: no message exists yet to read field constraints from, so
        // the policy half ignores fields and can only ever be more permissive than the
        // interceptor, never less — a refusal there means no policy applies to this key for this
        // session at all, which Authorize would refuse for any values whatsoever. Which verdict
        // it is belongs to the manager and not here: a feature the install was not sold is
        // refused as itself, and the caller gets at this door the same answer the interceptor
        // behind it would have given.
        if (await security.PreRefusal(message.MessageType, session, context.RequestAborted).ConfigureAwait(false) is { } refusal)
        {
            throw new BusinessException(refusal);
        }
    }
}
