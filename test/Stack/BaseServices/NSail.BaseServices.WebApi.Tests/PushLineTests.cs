// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net;
using Microsoft.Extensions.DependencyInjection;
using NSail.Messaging;
using NSail.Messaging.Runtime;
using NSail.Messaging.Runtime.Publishing;
using NSail.Messaging.WebApi.Push;

namespace NSail.BaseServices.WebApi.Tests;

/// <summary>A line that is answered and hung up on must not reconnect without pause, and the
/// server's line must exist before the client can read.</summary>
public sealed class PushLineTests
{
    [Fact]
    public async Task AServerThatAnswersAndClosesIsNotKnockedOnWithoutPause()
    {
        var connects = 0;

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddMessaging();
        Messaging.Sse.Setup.AddPush(services, new Uri("http://push.test/"), options =>
        {
            options.HttpMessageHandlerFactory = () => new Closing(() => Interlocked.Increment(ref connects));
        });

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var feed = scope.ServiceProvider.GetRequiredService<PushFeed>();

        feed.Open();

        await Task.Delay(TimeSpan.FromSeconds(3));
        await feed.Close();

        // Now, now again (the ladder starts at zero), then 2 s; a storm would count thousands.
        Assert.InRange(Volatile.Read(ref connects), 2, 4);
    }

    [Fact]
    public async Task AnEventPublishedWhenTheHeadGoesOutIsOnTheLine()
    {
        var streams = new PushStreams();
        var written = new List<string>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await streams.Hold(
            "tenant",
            (frame, _) =>
            {
                written.Add(frame);

                // Stop after the first frame: the one published while the head went out.
                cts.Cancel();

                return Task.CompletedTask;
            },
            cts.Token,
            _ =>
            {
                streams.Send("tenant", "Rang", "\"hi\"");

                return Task.CompletedTask;
            });

        Assert.Single(written);
        Assert.Contains("event: Rang", written[0]);
    }

    sealed class Closing(Action seen) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            seen();

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(string.Empty) });
        }
    }
}
