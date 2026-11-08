// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
    // in the middle of their work. The one ceiling that reaches it is the tenant's request
    // budget, a rate over every send of the tenant (RequestBudgetTests).
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

    [Fact]
    public async Task Addresses_of_one_IPv6_64_share_a_budget_and_another_64_does_not()
    {
        await using var host = await ThrottleHost.Start();

        for (var attempt = 0; attempt < Capped.PerMinute; attempt++)
        {
            Assert.Equal(StatusCodes.Status200OK, await host.From($"2001:db8:1:2::{attempt + 1}", Capped.Path));
        }

        Assert.Equal(StatusCodes.Status429TooManyRequests, await host.From("2001:db8:1:2:ffff::9", Capped.Path));
        Assert.Equal(StatusCodes.Status200OK, await host.From("2001:db8:1:3::1", Capped.Path));
    }

    [Fact]
    public async Task A_forwarded_header_from_a_peer_that_is_not_a_trusted_proxy_is_ignored()
    {
        await using var host = await ThrottleHost.Start(forwarded: true, "10.0.0.0/8");

        for (var attempt = 0; attempt < Capped.PerMinute; attempt++)
        {
            Assert.Equal(StatusCodes.Status200OK, await host.From("203.0.113.7", Capped.Path, $"198.51.100.{attempt + 1}"));
        }

        Assert.Equal(StatusCodes.Status429TooManyRequests, await host.From("203.0.113.7", Capped.Path, "198.51.100.99"));
    }

    [Fact]
    public async Task With_no_proxy_named_no_forwarded_header_is_believed()
    {
        await using var host = await ThrottleHost.Start(forwarded: true);

        for (var attempt = 0; attempt < Capped.PerMinute; attempt++)
        {
            Assert.Equal(StatusCodes.Status200OK, await host.From("203.0.113.7", Capped.Path, $"198.51.100.{attempt + 1}"));
        }

        Assert.Equal(StatusCodes.Status429TooManyRequests, await host.From("203.0.113.7", Capped.Path, "198.51.100.99"));
    }

    [Fact]
    public async Task A_forwarded_header_from_a_trusted_proxy_names_the_caller()
    {
        await using var host = await ThrottleHost.Start(forwarded: true, "10.0.0.0/8");

        for (var attempt = 0; attempt < Capped.PerMinute; attempt++)
        {
            Assert.Equal(StatusCodes.Status200OK, await host.From("10.1.2.3", Capped.Path, "198.51.100.1"));
        }

        Assert.Equal(StatusCodes.Status429TooManyRequests, await host.From("10.1.2.3", Capped.Path, "198.51.100.1"));
        Assert.Equal(StatusCodes.Status200OK, await host.From("10.1.2.3", Capped.Path, "198.51.100.2"));
    }

    [Fact]
    public async Task A_trusted_proxy_named_by_its_address_names_the_caller()
    {
        await using var host = await ThrottleHost.Start(forwarded: true, "10.1.2.3");

        for (var attempt = 0; attempt < Capped.PerMinute; attempt++)
        {
            Assert.Equal(StatusCodes.Status200OK, await host.From("10.1.2.3", Capped.Path, "198.51.100.1"));
        }

        Assert.Equal(StatusCodes.Status429TooManyRequests, await host.From("10.1.2.3", Capped.Path, "198.51.100.1"));
        Assert.Equal(StatusCodes.Status200OK, await host.From("10.1.2.3", Capped.Path, "198.51.100.2"));
    }

    // Behind a proxy with the flag off every caller is the proxy, and the Stack cannot see the
    // proxy, so the warning is the only thing that tells the install its ceiling is shared.
    [Fact]
    public async Task With_forwarded_headers_off_outside_Development_startup_warns_that_callers_share_one_address()
    {
        var trail = new WallTrail();

        await using var host = await ThrottleHost.Start(Environments.Production, trail);

        var warning = Assert.Single(trail.Warnings, said => said.Contains("shares the proxy's address", StringComparison.Ordinal));

        Assert.Contains("ASPNETCORE_FORWARDEDHEADERS_ENABLED", warning, StringComparison.Ordinal);
        Assert.Contains("Security:TrustedProxies", warning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task With_forwarded_headers_off_in_Development_startup_says_nothing()
    {
        var trail = new WallTrail();

        await using var host = await ThrottleHost.Start(Environments.Development, trail);

        Assert.DoesNotContain(trail.Warnings, said => said.Contains("proxy", StringComparison.Ordinal));
    }

    [Fact]
    public async Task With_forwarded_headers_on_and_no_proxy_named_startup_warns_that_nothing_is_believed()
    {
        var trail = new WallTrail();

        await using var host = await ThrottleHost.Start(Environments.Production, trail, forwarded: true);

        var warning = Assert.Single(trail.Warnings, said => said.Contains("proxy", StringComparison.Ordinal));

        Assert.Contains("no forwarded header is believed", warning, StringComparison.Ordinal);
        Assert.Contains("HSTS", warning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task With_a_proxy_named_startup_says_nothing()
    {
        var trail = new WallTrail();

        await using var host = await ThrottleHost.Start(Environments.Production, trail, forwarded: true, "10.0.0.0/8");

        Assert.DoesNotContain(trail.Warnings, said => said.Contains("proxy", StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_identity_is_capped_across_addresses_and_the_address_ceiling_stays()
    {
        await using var host = await ThrottleHost.Start();

        for (var attempt = 0; attempt < Login.PerMinute; attempt++)
        {
            Assert.Equal(200, await host.Post($"10.0.0.{attempt + 1}", $"{{\"account\":\"Admin\",\"password\":\"p{attempt}\"}}"));
        }

        Assert.Equal(429, await host.Post("10.0.1.1", "{\"account\":\" admin \"}"));
        Assert.Equal(200, await host.Post("10.0.1.1", "{\"account\":\"other\"}"));

        for (var attempt = 0; attempt < Login.AddressPerMinute; attempt++)
        {
            Assert.Equal(200, await host.Post("10.0.2.1", $"{{\"account\":\"u{attempt}\"}}"));
        }

        Assert.Equal(429, await host.Post("10.0.2.1", "{\"account\":\"fresh\"}"));
    }

    [Fact]
    public async Task A_query_value_beside_a_json_body_does_not_pick_the_partition()
    {
        await using var host = await ThrottleHost.Start();

        for (var attempt = 0; attempt < Login.PerMinute; attempt++)
        {
            Assert.Equal(200, await host.Post($"10.0.0.{attempt + 1}", "{\"account\":\"admin\"}", $"?account=decoy{attempt}"));
        }

        Assert.Equal(429, await host.Post("10.0.1.1", "{\"account\":\"admin\"}", "?account="));
    }

    [Fact]
    public async Task A_send_whose_identity_cannot_be_read_shares_one_budget()
    {
        await using var host = await ThrottleHost.Start();

        var padded = "{\"pad\":\"" + new string('x', 20_000) + "\",\"account\":\"admin\"}";

        for (var attempt = 0; attempt < Login.PerMinute; attempt++)
        {
            Assert.Equal(200, await host.Post($"10.0.0.{attempt + 1}", attempt % 2 == 0 ? padded : "{\"password\":\"p\"}"));
        }

        Assert.Equal(429, await host.Post("10.0.1.1", padded));
        Assert.Equal(200, await host.Post("10.0.1.1", "{\"account\":\"admin\"}"));
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

[Http(Method.Post, Login.Path)]
[Throttled(Login.AddressPerMinute, Identity = nameof(Login.Account), IdentityPerMinute = Login.PerMinute)]
public sealed class Login : IMessage
{
    public const string Path = "api/probe/login";

    public const int PerMinute = 3;

    public const int AddressPerMinute = 6;

    public string? Account { get; set; }
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

    public static Task<ThrottleHost> Start(bool forwarded = false, params string[] trustedProxies)
    {
        return Start(WebApplication.CreateBuilder(), forwarded, trustedProxies);
    }

    public static Task<ThrottleHost> Start(string environment, WallTrail logs, bool forwarded = false, params string[] trustedProxies)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });

        builder.Logging.AddProvider(logs);

        return Start(builder, forwarded, trustedProxies);
    }

    static async Task<ThrottleHost> Start(WebApplicationBuilder builder, bool forwarded, string[] trustedProxies)
    {

        if (forwarded)
        {
            builder.Configuration["ASPNETCORE_FORWARDEDHEADERS_ENABLED"] = "true";
        }

        for (var index = 0; index < trustedProxies.Length; index++)
        {
            builder.Configuration[$"Security:TrustedProxies:{index}"] = trustedProxies[index];
        }

        builder.WebHost.UseTestServer();

        builder.AddBaseWebApi();

        // UseBaseWebApi's UseAuthentication needs the services to exist; nothing here signs in.
        builder.Services.AddAuthentication();

        builder.Services.AddTransient<IEndpointEntry>(_ => new Probe(typeof(Capped), Capped.Path));
        builder.Services.AddTransient<IEndpointEntry>(_ => new Probe(typeof(AlsoCapped), AlsoCapped.Path));
        builder.Services.AddTransient<IEndpointEntry>(_ => new Probe(typeof(Uncapped), Uncapped.Path));
        builder.Services.AddTransient<IEndpointEntry>(_ => new Probe(typeof(Login), Login.Path, post: true));

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
    public async Task<int> From(string address, string path, string? forwardedFor = null)
    {
        var context = await _app.GetTestServer().SendAsync(request =>
        {
            request.Request.Scheme = "https";
            request.Request.Method = HttpMethods.Get;
            request.Request.Host = new HostString("localhost");
            request.Request.Path = "/" + path;
            request.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(address);

            if (forwardedFor is not null)
            {
                request.Request.Headers["X-Forwarded-For"] = forwardedFor;
            }
        });

        return context.Response.StatusCode;
    }

    public async Task<int> Post(string address, string json, string query = "")
    {
        var context = await _app.GetTestServer().SendAsync(request =>
        {
            request.Request.Scheme = "https";
            request.Request.Method = HttpMethods.Post;
            request.Request.Host = new HostString("localhost");
            request.Request.Path = "/" + Login.Path;
            request.Request.QueryString = new QueryString(query);
            request.Request.ContentType = "application/json";
            request.Request.Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
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
    readonly bool _post;

    public Probe(Type message, string path, bool post = false)
    {
        _message = message;
        _path = path;
        _post = post;
    }

    public void Configure(IEndpointRouteBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var route = _post ? builder.MapPost(_path, () => Results.Ok()) : builder.MapGet(_path, () => Results.Ok());

        route.WithMetadata(new MessageEndpointMetadata(_message));
    }
}
