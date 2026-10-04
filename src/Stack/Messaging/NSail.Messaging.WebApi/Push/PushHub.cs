// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using NSail.Messaging.Runtime.Publishing;

namespace NSail.Messaging.WebApi.Push;

/// <summary>The server's end of <see cref="PushFeed"/>. A client says nothing on it; it only
/// listens, and what it hears is decided by the one group it joins on connect.</summary>
[Authorize]
public sealed class PushHub : Hub
{
    readonly PushAudience _audience;

    public PushHub(PushAudience audience)
    {
        _audience = audience;
    }

    // Asked through this invocation's own scope, never the connect request's: a long-polling
    // connection outlives the request that opened it, and the edge's answer for that request
    // (the tenant, entered before authentication) is the flow this scope inherits.
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, _audience.Current, Context.ConnectionAborted);

        await base.OnConnectedAsync();
    }
}
