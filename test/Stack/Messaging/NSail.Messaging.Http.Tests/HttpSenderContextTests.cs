// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.Messaging.Runtime.Context;
using System.Net;

namespace NSail.Messaging.Http.Tests;

// The send half of a delivery's context crossing a transport hop: the sender is already inside
// the context its pipeline opened, so it stamps what THIS send was called with and nothing is
// threaded through ISender to get it there.
public sealed class HttpSenderContextTests
{
    sealed class Ping : IMessage;

    sealed class PingSender : HttpSender<Ping>
    {
        public PingSender(IHttpClientFactory httpClientFactory, MessageContextAccessor deliveries)
            : base(httpClientFactory, deliveries)
        {
        }

        protected override string Name => "Directory";

        protected override HttpRequestMessage BuildMessage(Ping message)
        {
            return CreateBuilder()
                .Method(HttpMethod.Get)
                .Path("api/directory/ping")
                .Build();
        }
    }

    sealed class Delivery : MessageContextAccessor
    {
        readonly MessageContext? _context;

        public Delivery(IReadOnlyDictionary<string, string>? headers)
        {
            _context = headers is null ? null : new MessageContext(headers);
        }

        public override MessageContext? Context
        {
            get { return _context; }
        }
    }

    sealed class Capture : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    static async Task<HttpRequestMessage> Sent(IReadOnlyDictionary<string, string>? headers)
    {
        var capture = new Capture();

        var services = new ServiceCollection();

        services
            .AddHttpClient("Directory", client => client.BaseAddress = new Uri("https://localhost/"))
            .ConfigurePrimaryHttpMessageHandler(() => capture);

        using var provider = services.BuildServiceProvider();

        var sender = new PingSender(
            provider.GetRequiredService<IHttpClientFactory>(),
            new Delivery(headers));

        await sender.Send(new Ping());

        return capture.Request!;
    }

    static string? Header(HttpRequestMessage request, string name)
    {
        return request.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;
    }

    [Fact]
    public async Task ASendStampsEveryHeaderItsDeliveryCarries()
    {
        var request = await Sent(new Dictionary<string, string>
        {
            [MessageHeaders.Source] = "asker",
            ["Trace"] = "abc123",
        });

        Assert.Equal("asker", Header(request, $"{WireHeaders.Prefix}{MessageHeaders.Source}"));
        Assert.Equal("abc123", Header(request, $"{WireHeaders.Prefix}Trace"));
    }

    [Fact]
    public async Task ASendWithNoContextStampsNothing()
    {
        var request = await Sent(null);

        Assert.DoesNotContain(
            request.Headers,
            header => header.Key.StartsWith(WireHeaders.Prefix, StringComparison.OrdinalIgnoreCase));
    }

    // The tenant word is the transport's, written by the proxy: a delivery of that name would
    // shadow the install's own on the way out, so it is the one name that never rides.
    [Fact]
    public async Task ADeliveryNeverStampsTheTenantHeader()
    {
        var request = await Sent(new Dictionary<string, string>
        {
            ["Tenant"] = "someone-elses-install",
            [MessageHeaders.Source] = "asker",
        });

        Assert.Null(Header(request, WireHeaders.Tenant));
        Assert.Equal("asker", Header(request, $"{WireHeaders.Prefix}{MessageHeaders.Source}"));
    }
}
