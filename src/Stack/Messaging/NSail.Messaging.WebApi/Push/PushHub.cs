// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NSail.Messaging.Runtime.Publishing;

namespace NSail.Messaging.WebApi.Push;

/// <summary>The server's end of <see cref="PushFeed"/>. A client says nothing on it; it only
/// listens, and what it hears is decided by the one group it joins on connect.</summary>
[Authorize]
public sealed class PushHub : Hub
{
    readonly PushAudience _audience;
    readonly ILogger<PushHub> _logger;

    public PushHub(PushAudience audience, ILogger<PushHub> logger)
    {
        _audience = audience;
        _logger = logger;
    }

    // Asked through this invocation's own scope, never the connect request's: a long-polling
    // connection outlives the request that opened it, and the edge's answer for that request
    // (the tenant, entered before authentication) is the flow this scope inherits.
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, _audience.Current, Context.ConnectionAborted);

        // Authorized once, at connect; a right withdrawn since ends the connection.
        if (_audience.Watch() is { } holds)
        {
            _ = PushWatch.Over(holds, Context.Abort, _logger, Context.ConnectionAborted);
        }

        await base.OnConnectedAsync();
    }
}
