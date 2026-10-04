// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.Messaging.Http;
using NSail.Messaging.Runtime.Context;

namespace NSail.Messaging.Http.Tests;

public class HttpSenderTests
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

    static IHttpClientFactory CreateFactory()
    {
        return new ServiceCollection()
            .AddHttpClient()
            .BuildServiceProvider()
            .GetRequiredService<IHttpClientFactory>();
    }

    [Fact]
    public async Task Send_names_the_client_when_it_has_no_base_address()
    {
        var sender = new PingSender(CreateFactory(), new MessageContextAccessor());

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            sender.Send(new Ping()));

        Assert.Equal("ClientNotConfigured", exception.Code);
        Assert.Contains("Directory", exception.Issues[0].Message);
        Assert.Contains("Ping", exception.Issues[0].Message);
    }
}
