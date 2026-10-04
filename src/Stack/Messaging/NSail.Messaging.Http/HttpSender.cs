// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;
using NSail.Messaging.Runtime.Context;
using NSail.Messaging.Runtime.Sending;
using NSail.Problems;
using NSail.Serialization;
using System.Net.Http.Json;
using System.Text.Json;

namespace NSail.Messaging.Http;

public abstract class HttpSender
{
    protected HttpSender(IHttpClientFactory httpClientFactory, MessageContextAccessor deliveries)
    {
        HttpClientFactory = httpClientFactory;
        Deliveries = deliveries;
    }

    protected abstract string Name { get; }

    protected IHttpClientFactory HttpClientFactory { get; }

    protected MessageContextAccessor Deliveries { get; }

    protected virtual HttpRequestMessageBuilder CreateBuilder()
    {
        return new();
    }

    protected virtual JsonSerializerOptions SerializerOptions { get; } = JsonOptions.Wire;

    /// <summary>A sender always builds a relative URI, so a client with no base address can
    /// never reach anything. Failing here names the client, because the alternative is a
    /// request that never leaves the browser and a screen that renders empty.</summary>
    protected HttpClient CreateClient(string messageType)
    {
        var client = HttpClientFactory.CreateClient(Name);

        if (client.BaseAddress is null)
        {
            throw new BusinessException(MessagingProblem.ClientNotConfigured(Name, messageType));
        }

        return client;
    }

    /// <summary>Stamps the delivery's own headers on the request, each under its wire name, so
    /// the handler on the far side reads what the caller sent this send with and an app-to-api
    /// hop says the same thing an in-process one does. A delivery carrying none stamps none.</summary>
    protected HttpRequestMessage WithContext(HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // The accessor answers because the sender runs INSIDE the context the pipeline opened:
        // a transport sender owns no scope, so SendPipeline reaches it with the delivery still
        // live. Nothing is threaded through ISender, which would put the wire's concern in
        // every sender's signature.
        if (Deliveries.Context is not { } context)
        {
            return request;
        }

        foreach (var (name, value) in context.Headers)
        {
            if (WireHeaders.Name(name) is { } wire)
            {
                request.Headers.TryAddWithoutValidation(wire, value);
            }
        }

        return request;
    }

    protected virtual async Task HandleError(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content is null)
        {
            throw new BusinessException(NetworkProblem.RequestFailed(
                (int)response.StatusCode,
                NetworkFailureReason.EmptyContent
            ));
        }

        var bytes = await response.Content
            .ReadAsByteArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        if (bytes.Length == 0)
        {
            throw new BusinessException(NetworkProblem.RequestFailed(
                (int)response.StatusCode,
                NetworkFailureReason.EmptyContent
            ));
        }

        try
        {
            var problem = JsonSerializer.Deserialize<Problem>(bytes, JsonOptions.Wire);

            if (problem is not null)
            {
                throw new BusinessException(problem);
            }

            throw new BusinessException(NetworkProblem.RequestFailed(
                (int)response.StatusCode,
                NetworkFailureReason.DeserializationFailed
            ));
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            throw new BusinessException(NetworkProblem.RequestFailed(
                (int)response.StatusCode,
                NetworkFailureReason.DeserializationFailed
            ));
        }
    }

}

public abstract class HttpSender<TMessage> : HttpSender, ISender<TMessage>
    where TMessage : IMessage
{
    protected HttpSender(IHttpClientFactory httpClientFactory, MessageContextAccessor deliveries)
        : base(httpClientFactory, deliveries)
    {
    }

    protected abstract HttpRequestMessage BuildMessage(TMessage message);

    public async Task Send(TMessage message, CancellationToken cancellationToken = default)
    {
        var client = CreateClient(typeof(TMessage).Name);
        var requestMessage = WithContext(BuildMessage(message));

        using var response = await client.SendAsync(
            requestMessage,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        ).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            await HandleError(response, cancellationToken).ConfigureAwait(false);
        }
    }
}

public abstract class HttpSender<TMessage, TResult> : HttpSender, ISender<TMessage, TResult>
    where TMessage : IMessage<TResult>
{
    protected HttpSender(IHttpClientFactory httpClientFactory, MessageContextAccessor deliveries)
        : base(httpClientFactory, deliveries)
    {
    }

    protected abstract HttpRequestMessage BuildMessage(TMessage message);

    public async Task<TResult> Send(TMessage message, CancellationToken cancellationToken = default)
    {
        var client = CreateClient(typeof(TMessage).Name);
        var requestMessage = WithContext(BuildMessage(message));

        using var response = await client.SendAsync(
            requestMessage,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        ).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            await HandleError(response, cancellationToken).ConfigureAwait(false);
        }

        var result = await response.Content
            .ReadFromJsonAsync<TResult>(SerializerOptions, cancellationToken)
            .ConfigureAwait(false);

        return result!;
    }
}