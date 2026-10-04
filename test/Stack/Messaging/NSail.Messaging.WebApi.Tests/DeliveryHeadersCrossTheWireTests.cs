// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using NSail.Messaging.Runtime;
using NSail.Messaging.Runtime.Context;

namespace NSail.Messaging.WebApi.Tests;

/// <summary>nsail#394: a delivery's context says the same thing whichever transport carried it.
/// The caller names its headers on the Send, the generated sender stamps them, the generated
/// endpoint reads them back, and the handler on the far side — in another composition, with its
/// own container — asks its accessor and gets what was sent, having written nothing.
///
/// <para>Both ends are the real emitted code: one host maps this assembly's generated endpoints,
/// one client sends through this assembly's generated senders, and the hop between them is a
/// real request through the real pipeline. A fixture that posted the headers by hand would prove
/// its own arrangement.</para></summary>
public sealed class DeliveryHeadersCrossTheWireTests : IAsyncLifetime
{
    WebApplication _host = null!;
    ServiceProvider _client = null!;

    public async Task InitializeAsync()
    {
        _host = await StartHost();
        _client = Caller(_host.GetTestServer());
    }

    public async Task DisposeAsync()
    {
        await _client.DisposeAsync();
        await _host.StopAsync();
        await _host.DisposeAsync();
    }

    static async Task<WebApplication> StartHost()
    {
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseTestServer();

        builder.Services.AddMessaging();
        builder.Services.AddMessagingJson();
        builder.Services.AddProbeEndpoints();
        builder.Services.AddScoped<IHandler<ReadDelivery, string>, DeliveryProbeHandler>();

        var app = builder.Build();

        app.MapEndpoints();

        await app.StartAsync();

        return app;
    }

    // The caller is its own composition, exactly as a client host is: it registers no handler at
    // all and reaches the far side only through the generated sender. Which named client that
    // sender asks for is derived from the declaring class's Area (generation.md), so nothing here
    // writes the name — every name the factory hands out is pointed at the host under test.
    static ServiceProvider Caller(TestServer server)
    {
        var services = new ServiceCollection();

        services.AddMessaging();
        services.AddHttpClient();
        services.AddProbeClients();

        services.ConfigureAll<HttpClientFactoryOptions>(options =>
        {
            options.HttpClientActions.Add(client => client.BaseAddress = server.BaseAddress);
            options.HttpMessageHandlerBuilderActions.Add(
                builder => builder.PrimaryHandler = server.CreateHandler());
        });

        return services.BuildServiceProvider();
    }

    Task<string> Send(IReadOnlyDictionary<string, string>? headers)
    {
        return _client.GetRequiredService<Mediator>().Send(new ReadDelivery(), headers);
    }

    [Fact]
    public async Task AHeaderPassedToASendIsReadByTheHandlerAcrossTheHop()
    {
        var seen = await Send(new Dictionary<string, string>
        {
            [MessageHeaders.Source] = "asker",
            ["Trace"] = "abc123",
        });

        // Exactly these two: the request carried a Host, an Accept-Charset and the rest of what
        // HTTP needs, and none of them is a delivery's header. The prefix is what separates them.
        Assert.Equal($"Trace=abc123;{MessageHeaders.Source}=asker", seen);
    }

    [Fact]
    public async Task ASendWithNoHeadersArrivesWithNoContextRatherThanAnEmptyOne()
    {
        Assert.Equal(ReadDelivery.None, await Send(null));
    }

    // The transport's own word is not a delivery's: the proxy writes it and the install reads it
    // there, so it neither rides out with a send nor arrives as something a handler can read.
    [Fact]
    public async Task TheTenantHeaderNeverCrossesAsADeliverysOwn()
    {
        var seen = await Send(new Dictionary<string, string>
        {
            ["Tenant"] = "someone-elses-install",
            [MessageHeaders.Source] = "asker",
        });

        Assert.Equal($"{MessageHeaders.Source}=asker", seen);
    }
}
