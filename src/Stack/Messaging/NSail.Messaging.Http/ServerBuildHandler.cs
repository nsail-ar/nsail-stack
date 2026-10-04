// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Builds;
using NSail.Messaging.Runtime.Context;

namespace NSail.Messaging.Http;

/// <summary>Reads the build that answered off every response, under the client rather than
/// inside <see cref="HttpSender"/>: a refusal is a response too, and here the non-2xx path —
/// which leaves the sender through <c>HandleError</c> and never returns — is seen by
/// construction instead of by a second call site somebody has to remember.</summary>
public sealed class ServerBuildHandler : DelegatingHandler
{
    readonly ServerBuild _build;

    public ServerBuildHandler(ServerBuild build)
    {
        _build = build;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.Headers.TryGetValues(WireHeaders.Version, out var values))
        {
            _build.Answered(values.FirstOrDefault());
        }

        return response;
    }
}
