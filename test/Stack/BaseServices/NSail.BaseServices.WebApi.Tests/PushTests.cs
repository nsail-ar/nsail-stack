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
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSail.Messaging;
using NSail.Messaging.Annotations;
using NSail.Messaging.Runtime;
using NSail.Messaging.Runtime.Publishing;
using NSail.Messaging.WebApi.Push;

namespace NSail.BaseServices.WebApi.Tests;

/// <summary>nsail#1480: a server-side publish of a <c>[Pushed]</c> event reaches a signed-in
/// client's own Mediator, where a screen hears it with the Subscribe it already uses.
///
/// <para>Every fact runs over both transports (nsail#1850): the server maps both roads always
/// and only the client's composition picks, so neither one may answer differently. Both ends
/// are the shipped ones — <c>AddBaseWebApi</c>/<c>UseBaseWebApi</c> on the server, and on the
/// client the feed a browser runs, composed by its own <c>AddPush</c> with only the handler
/// swapped for the in-memory server's. What the push targets emit per message is registered
/// here by hand, in the exact shape the generator's own tests pin. The tenant wall over the
/// same push is <see cref="PushTenancyTests"/>, which needs the slot's Postgres.</para></summary>
public sealed class PushTests
{
    [Theory]
    [InlineData(Transport.Hub)]
    [InlineData(Transport.Sse)]
    public async Task APushedEventReachesTheSignedInClientsMediator(Transport transport)
    {
        await using var host = await PushHost.Start();
        await using var client = await host.Listen(transport);

        await host.Publish("the shop moved");

        var heard = await client.Next<Rang>();

        Assert.Equal("the shop moved", heard.Text);
    }

    // The client publishes from the closed list its composition registered, whatever the server
    // sends: a name it does not know is dropped. Sent FIRST on the same line, so the known one
    // arriving alone is proof the other was heard and not published.
    [Theory]
    [InlineData(Transport.Hub)]
    [InlineData(Transport.Sse)]
    public async Task ANameTheClientDidNotRegisterIsNeverPublished(Transport transport)
    {
        await using var host = await PushHost.Start();
        await using var client = await host.Listen(transport);

        await host.Whisper();
        await host.Publish("after the whisper");

        await client.Next<Rang>();

        Assert.DoesNotContain(client.Heard, message => message is Whispered);
    }

    // The push replays nothing it carried while the line was down, so the catch-up rests on one
    // promise: every time the line comes back, the feed says so (PushConnected) and every
    // listener re-reads. A drop the server forces must end in that event and a working line.
    //
    // A case per transport rather than a theory's two rows: CI holds the hub's out by its full
    // name while nsail#2082 finds why a line it cuts sometimes never comes back, and a row's
    // name carries its argument (.github/workflows/ci.yml).
    [Fact]
    public async Task ALineTheServerDropsComesBackAndSaysSo()
    {
        await ALineTheServerDropsComesBack(Transport.Hub);
    }

    [Fact]
    public async Task AnEventStreamTheServerDropsComesBackAndSaysSo()
    {
        await ALineTheServerDropsComesBack(Transport.Sse);
    }

    static async Task ALineTheServerDropsComesBack(Transport transport)
    {
        await using var host = await PushHost.Start();
        await using var client = await host.Listen(transport);

        var cut = await host.Drop();

        await client.Connected(times: 2, after: cut);

        // The second line is the client's word, and the server registers it after the handshake
        // the client returns on: a publish aimed at an audience the line has not joined yet
        // reaches nobody.
        await host.Held(lines: 2);

        await host.Publish("after the drop");

        Assert.Equal("after the drop", (await client.Next<Rang>()).Text);
    }

    // The push answers a signed-in session alone, and under api/ an anonymous connect is told so
    // rather than redirected to a sign-in page it cannot follow.
    [Theory]
    [InlineData(Transport.Hub)]
    [InlineData(Transport.Sse)]
    public async Task AnAnonymousConnectIsRefused(Transport transport)
    {
        await using var host = await PushHost.Start();

        Assert.Equal(HttpStatusCode.Unauthorized, await PushRefusal.Of(host.Server, transport));
    }
}

/// <summary>Which road a client listens over — the only thing that differs between the two, and
/// its own composition's to pick.</summary>
public enum Transport
{
    Hub,

    Sse,
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
    // Past the feed's third retry (2s, then 10s after it): a slow host can miss the first two,
    // and a patience shorter than the schedule fails a reconnect that was still on its way.
    static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    readonly ServiceProvider _services;
    readonly AsyncServiceScope _scope;
    readonly List<IDisposable> _subscriptions = [];
    readonly SemaphoreSlim _arrived = new(0);
    readonly SemaphoreSlim _connected = new(0);
    readonly PushTrail _trail;
    readonly Transport _transport;

    PushClient(ServiceProvider services, PushTrail trail, Transport transport)
    {
        _services = services;
        _scope = services.CreateAsyncScope();
        _trail = trail;
        _transport = transport;
    }

    public List<IMessage> Heard { get; } = [];

    Mediator Mediator
    {
        get { return _scope.ServiceProvider.GetRequiredService<Mediator>(); }
    }

    /// <summary>Opens the feed and returns once it is listening — which the feed itself says,
    /// through the event every listener catches up on.</summary>
    public static async Task<PushClient> Listen(TestServer server, Transport transport, string? ticket, string? tenant = null)
    {
        var services = new ServiceCollection();

        // The WebAssembly host composes logging before anything else; the feed says through
        // it what a listener that failed would otherwise keep quiet. A provider the suite keeps
        // is what a browser's console is: with none, a line that never came back took down with
        // it every word the feed said about why (nsail#2082).
        var trail = new PushTrail();

        services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.AddProvider(trail);
        });

        services.AddMessaging();

        // What a kit's Push.Clients target emits, for Rang alone.
        services.AddSingleton<PushedMessage>(new PushedMessage<Rang>());

        // Each AddPush is its own transport project's, and this one assembly composes both: the
        // call is named in full so nothing here turns on which using is in scope.
        if (transport is Transport.Hub)
        {
            Messaging.SignalR.Setup.AddPush(services, PushHost.Origin, options =>
            {
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;

                PushRefusal.Headers(options.Headers, ticket, tenant);
            });
        }
        else
        {
            Messaging.Sse.Setup.AddPush(services, PushHost.Origin, options =>
            {
                options.HttpMessageHandlerFactory = server.CreateHandler;

                PushRefusal.Headers(options.Headers, ticket, tenant);
            });
        }

        var client = new PushClient(services.BuildServiceProvider(), trail, transport);

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

    /// <summary>Waits until the feed has said it is listening this many times in all. What the
    /// server did just before is the caller's to pass: a wait that ends in nothing is read from
    /// its own message or not at all, and the two halves of a drop are each other's
    /// explanation.</summary>
    public async Task Connected(int times, string? after = null)
    {
        while (_connections < times)
        {
            if (!await _connected.WaitAsync(Patience))
            {
                throw new TimeoutException(string.Join(
                    Environment.NewLine,
                    $"The {_transport} feed said it was listening {_connections} time(s), not {times}, within {Patience.TotalSeconds:0}s.",
                    after is null ? "The server was not asked to do anything." : $"The server: {after}",
                    "The client:",
                    _trail.ToString()));
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
                throw new TimeoutException(string.Join(
                    Environment.NewLine,
                    $"Nothing of {typeof(TMessage).Name} arrived over the {_transport} feed within {Patience.TotalSeconds:0}s.",
                    "The client:",
                    _trail.ToString()));
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

/// <summary>What the server answers a connect it will not have, over either transport: the hub
/// throws the status out of its start, an event-stream request simply answers it.</summary>
static class PushRefusal
{
    public static async Task<HttpStatusCode?> Of(TestServer server, Transport transport, string? ticket = null, string? tenant = null)
    {
        if (transport is Transport.Hub)
        {
            var connection = new HubConnectionBuilder()
                .WithUrl(new Uri(PushHost.Origin, PushFeed.Path), options =>
                {
                    options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                    options.Transports = HttpTransportType.LongPolling;

                    Headers(options.Headers, ticket, tenant);
                })
                .Build();

            await using (connection)
            {
                var refused = await Assert.ThrowsAsync<HttpRequestException>(() => connection.StartAsync());

                return refused.StatusCode;
            }
        }

        using var client = server.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(PushHost.Origin, PushFeed.SsePath));

        foreach (var header in Headers(new Dictionary<string, string>(StringComparer.Ordinal), ticket, tenant))
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        return response.StatusCode;
    }

    /// <summary>What a browser would have sent by itself: the session's cookie, and the tenant
    /// the host it was served from stands for.</summary>
    public static IDictionary<string, string> Headers(IDictionary<string, string> headers, string? ticket, string? tenant)
    {
        if (ticket is not null)
        {
            headers["Cookie"] = ticket;
        }

        if (tenant is not null)
        {
            headers[TenancyMiddleware.TenantHeader] = tenant;
        }

        return headers;
    }
}

sealed class PushHost : IAsyncDisposable
{
    public static readonly Uri Origin = new("https://localhost/");

    readonly WebApplication _app;
    readonly Severance _severance;

    int _listening;

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

        severance.Watch(app);

        PushProbes.MapSignIn(app);
        PushProbes.MapPublishes(app);

        await app.StartAsync();

        return new PushHost(app, severance);
    }

    /// <summary>A signed-in client listening over this transport, handed back once the SERVER
    /// holds its line — never on the client's word alone, which is a line the server may not
    /// have registered yet and so an audience a publish does not reach.</summary>
    public async Task<PushClient> Listen(Transport transport)
    {
        var client = await PushClient.Listen(Server, transport, await SignIn());

        await Held(lines: ++_listening);

        return client;
    }

    /// <summary>The server cuts every line it is holding, as a restart or a proxy timeout would,
    /// and says what it cut. It waits for one if it holds none yet.</summary>
    public Task<string> Drop()
    {
        return _severance.Drop();
    }

    /// <summary>Waits until the server has accepted this many lines in all — a reconnect's line
    /// is the next one, and a line it has not accepted yet is in no audience.</summary>
    public Task Held(int lines)
    {
        return _severance.Held(lines);
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
    // What a kit's Push.Publishers target emits — for both probes, so it is the client's list
    // that decides what a browser publishes.
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

/// <summary>Holds every line the server accepted, over either transport, so a test can cut them
/// from the server's side — the one end a client cannot fake. A hub connection is taken by a
/// filter; an event-stream line IS its request, so it is taken as the request goes past and cut
/// through the one handle a request has on its own connection.
///
/// <para>The two ends do not agree on when a line exists, which is why nothing here is read
/// off the client's word (nsail#2082): the handshake a <c>HubConnection</c> returns on goes out
/// before the server runs the hub's <c>OnConnectedAsync</c>, so a client that says it is
/// listening may be a line this holds nothing of — and cutting what it does not hold cuts
/// nothing, which is a reconnect that never had anything to answer.</para></summary>
sealed class Severance : IHubFilter
{
    // The server's registration may lag the client's word by a whole scheduling delay on a
    // loaded runner, which is where the race was first seen; shorter than the client's patience
    // so a line that never arrives is named here rather than as a reconnect that never came.
    static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    readonly List<HubCallerContext> _hubs = [];
    readonly List<IHttpRequestLifetimeFeature> _streams = [];
    readonly SemaphoreSlim _accepted = new(0);

    int _lines;
    int _cut;

    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        // After the hub's own OnConnectedAsync, never before it: a line counts as accepted once
        // it has joined the audience a publish goes to, and the handshake the client counts as
        // connected is already on its way by the time any of this runs.
        await next(context);

        lock (_hubs)
        {
            _hubs.Add(context.Context);
        }

        _accepted.Release();
    }

    public void Watch(WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/" + PushFeed.SsePath)
                && context.Features.Get<IHttpRequestLifetimeFeature>() is { } line)
            {
                // Before next, not after it: the stream IS this request, and the request only
                // returns when the line is over.
                lock (_streams)
                {
                    _streams.Add(line);
                }

                _accepted.Release();
            }

            await next(context);
        });
    }

    /// <summary>Waits until the server has accepted this many lines in all. The count only ever
    /// grows, so the line a reconnect opens is the next one.</summary>
    public async Task Held(int lines)
    {
        while (_lines < lines)
        {
            if (!await _accepted.WaitAsync(Patience))
            {
                throw new TimeoutException($"The server accepted {_lines} line(s), not {lines}, within {Patience.TotalSeconds:0}s.");
            }

            _lines++;
        }
    }

    /// <summary>Cuts every line the server is holding, waiting first for one it has not cut
    /// yet — a drop that cut nothing is not a drop, and it reads as a feed that never came
    /// back.</summary>
    public async Task<string> Drop()
    {
        await Held(_cut + 1);

        return Cut();
    }

    string Cut()
    {
        int hubs;
        int streams;

        lock (_hubs)
        {
            hubs = _hubs.Count;

            foreach (var connection in _hubs)
            {
                connection.Abort();
            }

            _hubs.Clear();
        }

        lock (_streams)
        {
            streams = _streams.Count;

            foreach (var line in _streams)
            {
                line.Abort();
            }

            _streams.Clear();
        }

        // What a drop may wait for next is one line more than every line ever cut, counted here
        // rather than off the waits: a line accepted and cut without a wait of its own is still
        // a line this no longer holds.
        _cut += hubs + streams;

        return $"cut {hubs} hub line(s) and {streams} event stream(s).";
    }
}

/// <summary>What the client said while it ran, for a wait that ended in nothing to carry — which
/// is where a reconnect that never started, one still retrying and one the server refused differ.
/// The feed's own log alone: the vendor's <c>HubConnection</c> takes its logging from a service
/// collection of its own, and the only hand that could pass this one to it is
/// <c>PushConnection</c>'s published shape.</summary>
sealed class PushTrail : ILoggerProvider
{
    readonly List<string> _said = [];

    public ILogger CreateLogger(string categoryName)
    {
        return new Pen(this, categoryName);
    }

    public void Dispose()
    {
    }

    public override string ToString()
    {
        lock (_said)
        {
            return _said.Count is 0 ? "  said nothing." : string.Join(Environment.NewLine, _said);
        }
    }

    void Say(string line)
    {
        lock (_said)
        {
            _said.Add("  " + line);
        }
    }

    sealed class Pen : ILogger
    {
        readonly PushTrail _trail;
        readonly string _category;

        public Pen(PushTrail trail, string category)
        {
            _trail = trail;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var failure = exception is null ? string.Empty : $" — {exception.GetType().Name}: {exception.Message}";

            _trail.Say($"{logLevel} {_category.Split('.')[^1]}: {formatter(state, exception)}{failure}");
        }
    }
}
