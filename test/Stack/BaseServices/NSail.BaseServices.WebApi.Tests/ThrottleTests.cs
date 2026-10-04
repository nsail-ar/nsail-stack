// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSail.Messaging;
using NSail.Messaging.Annotations;
using NSail.Messaging.WebApi;

namespace NSail.BaseServices.WebApi.Tests;

/// <summary>nsail#899: a door reachable by somebody the install never authenticated needs a
/// ceiling, and the ceiling is declared on the message rather than on a path — the route is
/// derived from the message already, so a second declaration could only ever name an address
/// that had moved.
///
/// <para>The host is the real pipeline, called and not rebuilt: <c>AddBaseWebApi</c> and
/// <c>UseBaseWebApi</c>, with hand-mapped entries carrying the same
/// <c>MessageEndpointMetadata</c> the generator emits. What is under test is that the
/// middleware reads a declaration off that metadata, so a fixture that mapped its endpoints
/// some other way would prove its own arrangement.</para></summary>
public sealed class ThrottleTests
{
    [Fact]
    public async Task A_throttled_endpoint_answers_until_the_ceiling_and_then_refuses()
    {
        await using var host = await ThrottleHost.Start();

        for (var attempt = 0; attempt < Capped.PerMinute; attempt++)
        {
            using var allowed = await host.Get(Capped.Path);

            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        using var refused = await host.Get(Capped.Path);

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal("TooManyRequests", await Code(refused));
    }

    // A message that declares no ceiling is not throttled at all: everything behind a policy is
    // already bounded by who holds the grant, and a limit there would only refuse a customer
    // in the middle of their work.
    [Fact]
    public async Task An_endpoint_whose_message_declares_nothing_is_never_refused()
    {
        await using var host = await ThrottleHost.Start();

        for (var attempt = 0; attempt < Capped.PerMinute * 3; attempt++)
        {
            using var response = await host.Get(Uncapped.Path);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    // One budget for every public door would let a flood on one of them close the others.
    [Fact]
    public async Task Each_message_spends_a_budget_of_its_own()
    {
        await using var host = await ThrottleHost.Start();

        for (var attempt = 0; attempt < Capped.PerMinute; attempt++)
        {
            using var spent = await host.Get(Capped.Path);

            Assert.Equal(HttpStatusCode.OK, spent.StatusCode);
        }

        using var other = await host.Get(AlsoCapped.Path);

        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
    }

    // And a budget per door alone would be a budget the whole internet spends together: the
    // caller who has not asked yet is served while the one hammering is refused.
    [Fact]
    public async Task Each_caller_spends_a_budget_of_its_own()
    {
        await using var host = await ThrottleHost.Start();

        for (var attempt = 0; attempt < Capped.PerMinute; attempt++)
        {
            Assert.Equal(StatusCodes.Status200OK, await host.From("203.0.113.7", Capped.Path));
        }

        Assert.Equal(StatusCodes.Status429TooManyRequests, await host.From("203.0.113.7", Capped.Path));
        Assert.Equal(StatusCodes.Status200OK, await host.From("203.0.113.8", Capped.Path));
    }

    static async Task<string> Code(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.GetProperty("code").GetString() ?? string.Empty;
    }
}

// The ceiling is low on purpose: what the suite proves is that there IS one and where it
// is read from, and a realistic number would only make the loop longer.
[Http(Method.Get, Capped.Path)]
[Throttled(Capped.PerMinute)]
public sealed class Capped : IMessage
{
    public const string Path = "api/probe/capped";

    public const int PerMinute = 3;
}

[Http(Method.Get, AlsoCapped.Path)]
[Throttled(Capped.PerMinute)]
public sealed class AlsoCapped : IMessage
{
    public const string Path = "api/probe/also-capped";
}

[Http(Method.Get, Uncapped.Path)]
public sealed class Uncapped : IMessage
{
    public const string Path = "api/probe/uncapped";
}

sealed class ThrottleHost : IAsyncDisposable
{
    readonly WebApplication _app;

    ThrottleHost(WebApplication app)
    {
        _app = app;
    }

    HttpClient Client { get; set; } = null!;

    public static async Task<ThrottleHost> Start()
    {
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseTestServer();

        builder.AddBaseWebApi();

        // UseBaseWebApi's UseAuthentication needs the services to exist; nothing here signs in.
        builder.Services.AddAuthentication();

        builder.Services.AddTransient<IEndpointEntry>(_ => new Probe(typeof(Capped), Capped.Path));
        builder.Services.AddTransient<IEndpointEntry>(_ => new Probe(typeof(AlsoCapped), AlsoCapped.Path));
        builder.Services.AddTransient<IEndpointEntry>(_ => new Probe(typeof(Uncapped), Uncapped.Path));

        var app = builder.Build();

        app.UseBaseWebApi();

        await app.StartAsync();

        // https, so UseHttpsRedirection answers nothing and every probe reaches its endpoint.
        return new ThrottleHost(app)
        {
            Client = app.GetTestClient(),
        };
    }

    public Task<HttpResponseMessage> Get(string path)
    {
        return Client.GetAsync(new Uri($"https://localhost/{path}"));
    }

    /// <summary>The same request from a named address. The client cannot state one — TestServer
    /// leaves the connection bare — so the context is built by hand, which is the only way the
    /// per-caller half of the partition can be asked at all.</summary>
    public async Task<int> From(string address, string path)
    {
        var context = await _app.GetTestServer().SendAsync(request =>
        {
            request.Request.Scheme = "https";
            request.Request.Method = HttpMethods.Get;
            request.Request.Host = new HostString("localhost");
            request.Request.Path = "/" + path;
            request.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(address);
        });

        return context.Response.StatusCode;
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();

        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}

// What the generated entry does, in the smallest shape that still carries the one thing the
// middleware reads: the message the matched route names.
sealed class Probe : IEndpointEntry
{
    readonly Type _message;
    readonly string _path;

    public Probe(Type message, string path)
    {
        _message = message;
        _path = path;
    }

    public void Configure(IEndpointRouteBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.MapGet(_path, () => Results.Ok())
            .WithMetadata(new MessageEndpointMetadata(_message));
    }
}
