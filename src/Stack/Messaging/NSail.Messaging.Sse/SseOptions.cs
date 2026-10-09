// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging.Sse;

/// <summary>What a composition may add under the line a browser opens by itself: the handler it
/// goes out through — a test hands over its in-memory server's — and headers the browser would
/// have sent on its own.</summary>
public sealed class SseOptions
{
    public Func<HttpMessageHandler>? HttpMessageHandlerFactory { get; set; }

    public IDictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
