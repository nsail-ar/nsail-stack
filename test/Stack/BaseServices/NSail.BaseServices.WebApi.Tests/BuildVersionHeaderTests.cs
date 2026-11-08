// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net;
using NSail.Builds;
using NSail.Data;
using NSail.Messaging.Runtime.Context;

namespace NSail.BaseServices.WebApi.Tests;

// The server half of a deploy reaching a client that is already open (nsail#1452): nothing asks
// for the build the server is running, so every answer carries it. Run through the real
// pipeline, because the two things that could drop it are pipeline facts — where the middleware
// stands, and that ErrorMiddleware clears the response it rewrites.
public sealed class BuildVersionHeaderTests : IAsyncLifetime
{
    TenancyHost _host = null!;

    public async Task InitializeAsync()
    {
        _host = await TenancyHost.Start(TenancyMode.None);
    }

    public async Task DisposeAsync()
    {
        await _host.DisposeAsync();
    }

    [Fact]
    public async Task ASignedInCallerIsAnsweredWithTheBuildThatGaveIt()
    {
        var ticket = await _host.SignIn(tenant: null);

        using var response = await _host.Client.SendAsync(_host.Get("/where", tenant: null, ticket: ticket));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(BuildVersion.Current, Version(response));
    }

    [Fact]
    public async Task AnAnonymousCallerIsNotToldTheBuild()
    {
        using var response = await _host.Client.SendAsync(_host.Get("/where", tenant: null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(Version(response));
    }

    [Fact]
    public async Task EveryAnswerCarriesTheBrowserHardeningHeaders()
    {
        using var response = await _host.Client.SendAsync(_host.Get("/where", tenant: null));

        Assert.Equal("nosniff", Single(response, "X-Content-Type-Options"));
        Assert.Equal(ResponseHeadersMiddleware.ReferrerPolicy, Single(response, "Referrer-Policy"));
        Assert.Equal(ResponseHeadersMiddleware.PermissionsPolicy, Single(response, "Permissions-Policy"));
    }

    [Fact]
    public async Task ARefusalCarriesThemToo()
    {
        using var response = await _host.Client.SendAsync(_host.Get("/guarded", tenant: null));

        Assert.Equal("nosniff", Single(response, "X-Content-Type-Options"));
    }

    static string? Single(HttpResponseMessage response, string name)
    {
        return response.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;
    }

    // The answer a client running last week's build is likeliest to get, and the one a header
    // written on the way in would never reach.
    [Fact]
    public async Task AProblemTheErrorHandlerRewroteCarriesItToo()
    {
        var ticket = await _host.SignIn(tenant: null);

        using var response = await _host.Client.SendAsync(_host.Get("/boom", tenant: null, ticket: ticket));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(BuildVersion.Current, Version(response));
    }

    // The word the transport speaks on the way back is one a delivery may not speak on the way
    // out, exactly as the tenant word is. Held equal here, where the middleware that writes it
    // lives, so renaming one end cannot go quiet.
    [Fact]
    public void TheNameTheServerAnswersWithIsOneADeliveryMayNotCarry()
    {
        Assert.Null(WireHeaders.Name("Version"));
        Assert.Null(WireHeaders.Logical(WireHeaders.Version));
    }

    static string? Version(HttpResponseMessage response)
    {
        return response.Headers.TryGetValues(WireHeaders.Version, out var values)
            ? string.Join(",", values)
            : null;
    }
}
