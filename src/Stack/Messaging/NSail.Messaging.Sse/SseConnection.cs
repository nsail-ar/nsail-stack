// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net.Http.Headers;
using NSail.Messaging.Runtime.Publishing;

namespace NSail.Messaging.Sse;

/// <summary>Where the feed listens and over what. The address is the host's own origin plus
/// <see cref="PushFeed.SsePath"/>, so nothing about it is configured, and a same-origin request
/// carries the session's own cookie without being told to.</summary>
public sealed class SseConnection
{
    readonly Uri _address;
    readonly SseOptions _options = new();

    public SseConnection(Uri origin, Action<SseOptions>? transport = null)
    {
        ArgumentNullException.ThrowIfNull(origin);

        _address = new Uri(origin, PushFeed.SsePath);

        transport?.Invoke(_options);
    }

    public HttpClient Build()
    {
        var client = _options.HttpMessageHandlerFactory is { } handler
            ? new HttpClient(handler(), disposeHandler: true)
            : new HttpClient();

        // The body of an event-stream never ends on purpose, and the default timeout covers
        // reading it: left on, it would cut every line after 100 seconds.
        client.Timeout = Timeout.InfiniteTimeSpan;

        return client;
    }

    public HttpRequestMessage Open()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, _address);

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        // In Blazor WebAssembly a response is buffered whole unless streaming is turned on for
        // the request, so an endless body yields nothing at all. The browser's handler reads
        // this option by name, which is how it is set without taking a reference on
        // Microsoft.AspNetCore.Components.WebAssembly for the extension method that sets it.
        request.Options.Set(new HttpRequestOptionsKey<bool>("WebAssemblyEnableStreamingResponse"), true);

        foreach (var header in _options.Headers)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return request;
    }
}
