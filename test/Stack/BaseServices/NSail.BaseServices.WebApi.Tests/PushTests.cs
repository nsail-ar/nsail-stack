// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSail.Messaging;
using NSail.Messaging.Annotations;
using NSail.Messaging.Runtime;
using NSail.Messaging.Runtime.Publishing;
using NSail.Messaging.SignalR;
using NSail.Messaging.WebApi.Push;

namespace NSail.BaseServices.WebApi.Tests;

/// <summary>nsail#1480: a server-side publish of a <c>[Pushed]</c> event reaches a signed-in
/// client's own Mediator, where a screen hears it with the Subscribe it already uses.
///
/// <para>Both ends are the shipped ones: <c>AddBaseWebApi</c>/<c>UseBaseWebApi</c> on the server,
/// and on the client the <c>HubFeed</c> a browser runs, composed by <c>AddPush</c> with only
/// the transport swapped for the in-memory server's. What the SignalR targets emit per message
/// is registered here by hand, in the exact shape the generator's own tests pin. The tenant
/// wall over the same push is <see cref="PushTenancyTests"/>, which needs the slot's
/// Postgres.</para></summary>
public sealed class PushTests
{
    [Fact]
    public async Task APushedEventReachesTheSignedInClientsMediator()
    {
        await using var host = await PushHost.Start();
        await using var client = await PushClient.Listen(host.Server, await host.SignIn());

        await host.Publish("the shop moved");

        var heard = await client.Next<Rang>();

        Assert.Equal("the shop moved", heard.Text);
    }

    // The client publishes from the closed list its composition registered, whatever the server
    // sends: a name it does not know is dropped. Sent FIRST on the same line, so the known one
    // arriving alone is proof the other was heard and not published.
    [Fact]
    public async Task ANameTheClientDidNotRegisterIsNeverPublished()
    {
        await using var host = await PushHost.Start();
        await using var client = await PushClient.Listen(host.Server, await host.SignIn());

        await host.Whisper();
        await host.Publish("after the whisper");

        await client.Next<Rang>();

        Assert.DoesNotContain(client.Heard, message => message is Whispered);
    }

    // The push replays nothing it carried while the line was down, so the catch-up rests on one
    // promise: every time the line comes back, the feed says so (PushConnected) and every
    // listener re-reads. A drop the server forces must end in that event and a working line.
    [Fact]
    public async Task ALineTheServerDropsComesBackAndSaysSo()
    {
        await using var host = await PushHost.Start();
        await using var client = await PushClient.Listen(host.Server, await host.SignIn());

        await host.Drop();

        await client.Connected(times: 2);

        await host.Publish("after the drop");

        Assert.Equal("after the drop", (await client.Next<Rang>()).Text);
    }

    // The hub answers a signed-in session alone, and under api/ an anonymous connect is told so
    // rather than redirected to a sign-in page it cannot follow.
    [Fact]
    public async Task AnAnonymousConnectIsRefused()
    {
        await using var host = await PushHost.Start();

        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(PushHost.Origin, PushFeed.Path), options =>
            {
                options.HttpMessageHandlerFactory = _ => host.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();

        await using (connection)
        {
            var refused = await Assert.ThrowsAsync<HttpRequestException>(() => connection.StartAsync());

            Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        }
    }
}

[Pushed]
public sealed class Rang : IMessage
{
    public string Text { get; init; } = string.Empty;
}

[Pushed]
public sealed class Whispered : IMessage
{
}

/// <summary>The browser's half: a scope of its own, as the renderer's is, with the feed open
/// and every pushed event it publishes recorded through an ordinary subscription.</summary>
sealed class PushClient : IAsyncDisposable
{
    // Past HubFeed's third retry (2s, then 10s after it): a slow host can miss the first two,
    // and a patience shorter than the schedule fails a reconnect that was still on its way.
    static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    readonly ServiceProvider _services;
    readonly AsyncServiceScope _scope;
    readonly List<IDisposable> _subscriptions = [];
    readonly SemaphoreSlim _arrived = new(0);
    readonly SemaphoreSlim _connected = new(0);

    PushClient(ServiceProvider services)
    {
        _services = services;
        _scope = services.CreateAsyncScope();
    }

    public List<IMessage> Heard { get; } = [];

    Mediator Mediator
    {
        get { return _scope.ServiceProvider.GetRequiredService<Mediator>(); }
    }

    /// <summary>Opens the feed and returns once it is listening — which the feed itself says,
    /// through the event every listener catches up on.</summary>
    public static async Task<PushClient> Listen(TestServer server, string? ticket, string? tenant = null)
    {
        var services = new ServiceCollection();

        // The WebAssembly host composes logging before anything else; the feed says through
        // it what a listener that failed would otherwise keep quiet.
        services.AddLogging();
        services.AddMessaging();

        // What a kit's SignalR.Clients target emits, for Rang alone.
        services.AddSingleton<PushedMessage>(new PushedMessage<Rang>());

        services.AddPush(PushHost.Origin, options =>
        {
            options.HttpMessageHandlerFactory = _ => server.CreateHandler();
            options.Transports = HttpTransportType.LongPolling;

            if (ticket is not null)
            {
                options.Headers["Cookie"] = ticket;
            }

            if (tenant is not null)
            {
                options.Headers[TenancyMiddleware.TenantHeader] = tenant;
            }
        });

        var client = new PushClient(services.BuildServiceProvider());

        client._subscriptions.Add(client.Mediator.Subscribe<PushConnected>((_, _) =>
        {
            client._connected.Release();

            return Task.CompletedTask;
        }));

        client._subscriptions.Add(client.Mediator.Subscribe<Rang>((message, _) => client.Record(message)));
        client._subscriptions.Add(client.Mediator.Subscribe<Whispered>((message, _) => client.Record(message)));

        client._scope.ServiceProvider.GetRequiredService<PushFeed>().Open();

        await client.Connected(times: 1);

        return client;
    }

    int _connections;

    /// <summary>Waits until the feed has said it is listening this many times in all.</summary>
    public async Task Connected(int times)
    {
        while (_connections < times)
        {
            if (!await _connected.WaitAsync(Patience))
            {
                throw new TimeoutException($"The feed said it was listening {_connections} time(s), not {times}.");
            }

            _connections++;
        }
    }

    public async Task<TMessage> Next<TMessage>()
        where TMessage : IMessage
    {
        while (true)
        {
            lock (Heard)
            {
                if (Heard.OfType<TMessage>().FirstOrDefault() is { } found)
                {
                    return found;
                }
            }

            if (!await _arrived.WaitAsync(Patience))
            {
                throw new TimeoutException($"Nothing of {typeof(TMessage).Name} arrived.");
            }
        }
    }

    Task Record(IMessage message)
    {
        lock (Heard)
        {
            Heard.Add(message);
        }

        _arrived.Release();

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }

        await _scope.DisposeAsync();
        await _services.DisposeAsync();
    }
}

sealed class PushHost : IAsyncDisposable
{
    public static readonly Uri Origin = new("https://localhost/");

    readonly WebApplication _app;
    readonly Severance _severance;

    PushHost(WebApplication app, Severance severance)
    {
        _app = app;
        _severance = severance;
    }

    public TestServer Server
    {
        get { return _app.GetTestServer(); }
    }

    public static async Task<PushHost> Start()
    {
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseTestServer();
        builder.AddBaseWebApi();

        PushProbes.Register(builder.Services);

        var severance = new Severance();

        builder.Services.AddSignalR(options => options.AddFilter(severance));

        builder.Services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;

                    return Task.CompletedTask;
                };
            });

        var app = builder.Build();

        app.UseBaseWebApi();

        PushProbes.MapSignIn(app);
        PushProbes.MapPublishes(app);

        await app.StartAsync();

        return new PushHost(app, severance);
    }

    /// <summary>The server cuts every open line, as a restart or a proxy timeout would.</summary>
    public Task Drop()
    {
        _severance.Drop();

        return Task.CompletedTask;
    }

    public async Task<string> SignIn()
    {
        using var client = Server.CreateClient();

        using var response = await client.GetAsync(new Uri(Origin, "sign-in"));

        response.EnsureSuccessStatusCode();

        return response.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
    }

    public async Task Publish(string text)
    {
        using var client = Server.CreateClient();

        using var response = await client.PostAsync(new Uri(Origin, $"rang?text={Uri.EscapeDataString(text)}"), null);

        response.EnsureSuccessStatusCode();
    }

    public async Task Whisper()
    {
        using var client = Server.CreateClient();

        using var response = await client.PostAsync(new Uri(Origin, "whispered"), null);

        response.EnsureSuccessStatusCode();
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}

/// <summary>The server's half of the probes, mapped into whichever host a suite stands: a
/// sign-in in miniature (TenancyHost brings its own) and the two publishes, each made the way a handler makes one — through
/// the request's own Mediator.</summary>
static class PushProbes
{
    // What a kit's SignalR.Hubs target emits — for both probes, so it is the client's list that
    // decides what a browser publishes.
    public static void Register(IServiceCollection services)
    {
        services.AddScoped<IPublisher<Rang>, PushPublisher<Rang>>();
        services.AddScoped<IPublisher<Whispered>, PushPublisher<Whispered>>();
    }

    public static void MapSignIn(WebApplication app)
    {
        app.MapGet("/sign-in", async (HttpContext context) =>
        {
            await context.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(new ClaimsIdentity([], CookieAuthenticationDefaults.AuthenticationScheme)));

            return Results.Ok();
        });
    }

    public static void MapPublishes(WebApplication app)
    {
        app.MapPost("/rang", async (string text, Mediator mediator) =>
        {
            await mediator.Publish(new Rang { Text = text });

            return Results.Ok();
        });

        app.MapPost("/whispered", async (Mediator mediator) =>
        {
            await mediator.Publish(new Whispered());

            return Results.Ok();
        });
    }
}

/// <summary>Holds every connection the hub accepted, so a test can cut them from the server's
/// side — the one end a client cannot fake.</summary>
sealed class Severance : Microsoft.AspNetCore.SignalR.IHubFilter
{
    readonly List<Microsoft.AspNetCore.SignalR.HubCallerContext> _open = [];

    public async Task OnConnectedAsync(Microsoft.AspNetCore.SignalR.HubLifetimeContext context, Func<Microsoft.AspNetCore.SignalR.HubLifetimeContext, Task> next)
    {
        lock (_open)
        {
            _open.Add(context.Context);
        }

        await next(context);
    }

    public void Drop()
    {
        lock (_open)
        {
            foreach (var connection in _open)
            {
                connection.Abort();
            }

            _open.Clear();
        }
    }
}
