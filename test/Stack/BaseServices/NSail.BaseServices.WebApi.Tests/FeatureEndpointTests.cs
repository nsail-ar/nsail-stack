// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSail.Messaging;
using NSail.Messaging.Annotations;
using NSail.Messaging.Runtime;
using NSail.Messaging.WebApi;
using NSail.Security;
using NSail.Security.Annotations;

namespace NSail.BaseServices.WebApi.Tests;

/// <summary>What a CALLER actually meets at the door when the install was not sold the feature
/// a message belongs to. There are two gates in front of a handler and the one that answers
/// first is this host's — <c>EndpointGateMiddleware</c>, ahead of model binding, where no
/// message instance exists yet — so proving the verdict on the interceptor alone proves it on
/// code a marked message never reaches.
///
/// <para>The host is the real pipeline, called and not rebuilt: <c>AddBaseWebApi</c> and
/// <c>UseBaseWebApi</c> with the gate turned on, and hand-mapped entries carrying the same
/// <c>MessageEndpointMetadata</c> the generator emits (the shape <see cref="ThrottleTests"/>
/// uses, and its <c>Probe</c>).</para></summary>
public sealed class FeatureEndpointTests
{
    const string Off = """{"whatsapp": false}""";

    const string On = """{"whatsapp": true}""";

    /// <summary>The one the story bought: a 403 that names the FEATURE, because no grant anybody
    /// can be given widens a feature nobody paid for — "you do not have permission" would send
    /// the caller to an administrator who cannot help them.</summary>
    [Fact]
    public async Task A_send_whose_feature_the_install_does_not_have_is_refused_as_the_feature()
    {
        await using var host = await FeatureHost.Start(Off);

        using var response = await host.Get(Sold.Path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("FeatureUnavailable", await Code(response));
        Assert.Equal(Sold.Feature, await Source(response));
    }

    /// <summary>And it is the awaited read behind it, not the optimistic one: the bag a screen
    /// derives its visibility from may not be in hand yet, and a door that assumed it would let
    /// the send through to be refused one gate later with the other verdict.</summary>
    [Fact]
    public async Task The_door_asks_the_bag_rather_than_assuming_it()
    {
        await using var host = await FeatureHost.Start(Off, inHand: false);

        using var response = await host.Get(Sold.Path);

        Assert.Equal("FeatureUnavailable", await Code(response));
    }

    [Theory]
    [InlineData(On)]
    [InlineData("{}")]
    [InlineData(null)]
    public async Task A_send_the_bag_does_not_close_reaches_its_endpoint(string? bag)
    {
        await using var host = await FeatureHost.Start(bag);

        using var response = await host.Get(Sold.Path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>The other half of the same door, which this one must not have swallowed: a
    /// refusal by permission is still answered as the permission it is.</summary>
    [Fact]
    public async Task A_send_no_policy_allows_is_still_refused_as_a_permission()
    {
        await using var host = await FeatureHost.Start(On);

        using var response = await host.Get(Ungranted.Path);

        Assert.Equal("Unauthorized", await Code(response));
    }

    static async Task<string> Code(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.GetProperty("code").GetString() ?? string.Empty;
    }

    static async Task<string> Source(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.GetProperty("issues")[0].GetProperty("source").GetString() ?? string.Empty;
    }
}

[Http(Method.Get, Sold.Path)]
[RequiresFeature(Sold.Feature)]
public sealed class Sold : IMessage
{
    public const string Path = "api/probe/sold";

    public const string Feature = "whatsapp";
}

// Marked by nothing and granted by nothing: what the door answered before a feature existed.
[Http(Method.Get, Ungranted.Path)]
public sealed class Ungranted : IMessage
{
    public const string Path = "api/probe/ungranted";
}

/// <summary>A bag written for this scope, with the two reads told apart: <c>inHand</c> false is
/// the scope whose row has not been read yet, which is what <c>FeatureProvider.Assumed</c>
/// answers unrestricted for.</summary>
internal sealed class FixedFeatureProvider : FeatureProvider
{
    readonly Entitlements _bag;
    readonly bool _inHand;

    public FixedFeatureProvider(string? bag, bool inHand)
    {
        _bag = Entitlements.Read(bag);
        _inHand = inHand;
    }

    public override Task<Entitlements> Current(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_bag);
    }

    public override Entitlements Assumed
    {
        get { return _inHand ? _bag : Entitlements.Unrestricted; }
    }
}

sealed class FeatureHost : IAsyncDisposable
{
    readonly WebApplication _app;

    FeatureHost(WebApplication app)
    {
        _app = app;
    }

    HttpClient Client { get; set; } = null!;

    public static async Task<FeatureHost> Start(string? bag, bool inHand = true)
    {
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseTestServer();

        builder.AddBaseWebApi();

        // UseBaseWebApi's UseAuthentication needs the services to exist; nothing here signs in.
        builder.Services.AddAuthentication();

        // The gate itself: without the enforcement marker the door lets everything past, which
        // is how a client composes the same pipeline.
        builder.Services.AddSecurityEnforcement();

        // After AddBaseWebApi's own registration, so the bag under test is the one answered
        // rather than the row's (there is no store here to hold a row).
        builder.Services.AddScoped<FeatureProvider>(_ => new FixedFeatureProvider(bag, inHand));

        builder.Services.AddSingleton(new MessageRegistration(typeof(Sold)));
        builder.Services.AddSingleton(new MessageRegistration(typeof(Ungranted)));
        builder.Services.AddSingleton(new PolicyHandlerFactory(typeof(Sold), policy => new GrantedPolicyHandler(policy)));
        builder.Services.AddSingleton(new PolicyHandlerFactory(typeof(Ungranted), policy => new GrantedPolicyHandler(policy)));

        // A grant wide enough that nothing but the feature can be what refuses the send, named
        // through the registry so the key is the one the gate asks and not a copy of it.
        builder.Services.AddSingleton(provider => new Policy
        {
            Name = "Anyone may send this",
            Messages = [provider.GetRequiredService<MessageRegistry>().KeyFor(typeof(Sold))],
        });

        builder.Services.AddTransient<IEndpointEntry>(_ => new Probe(typeof(Sold), Sold.Path));
        builder.Services.AddTransient<IEndpointEntry>(_ => new Probe(typeof(Ungranted), Ungranted.Path));

        var app = builder.Build();

        app.UseBaseWebApi();

        await app.StartAsync();

        return new FeatureHost(app)
        {
            Client = app.GetTestClient(),
        };
    }

    // https, so UseHttpsRedirection answers nothing and every probe reaches its endpoint.
    public Task<HttpResponseMessage> Get(string path)
    {
        return Client.GetAsync(new Uri($"https://localhost/{path}"));
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();

        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}

// The generated handler for a message with no constrainable field: the key and the audience are
// the whole question, and the audience here is everyone.
sealed class GrantedPolicyHandler : PolicyHandler
{
    public GrantedPolicyHandler(Policy policy)
        : base(policy)
    {
    }

    public override Task<bool> Authorize(object message, Session session, RelationProvider relations, CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }
}
