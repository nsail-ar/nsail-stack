// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSail.Messaging.Runtime;

namespace NSail.Messaging.WebApi.Tests;

/// <summary>A long and a decimal cross a generated endpoint as strings, every digit kept, and a
/// text that is not the number is a malformed body — the 400 a broken JSON number gets.</summary>
public sealed class ExactNumbersCrossTheWireTests : IAsyncLifetime
{
    WebApplication _host = null!;
    HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseTestServer();

        builder.Services.AddMessaging();
        builder.Services.AddMessagingJson();
        builder.Services.AddProbeEndpoints();
        builder.Services.AddScoped<IHandler<EchoExact, EchoExact>, EchoExactHandler>();

        _host = builder.Build();
        _host.MapEndpoints();

        await _host.StartAsync();

        _client = _host.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _host.StopAsync();
        await _host.DisposeAsync();
    }

    Task<HttpResponseMessage> Post(string json)
    {
        return _client.PostAsync("api/probe/exact", new StringContent(json, Encoding.UTF8, "application/json"));
    }

    [Fact]
    public async Task EveryDigitComesBack()
    {
        var response = await Post("{\"id\":\"9007199254740993\",\"price\":\"1234567890.123456789\"}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("{\"id\":\"9007199254740993\",\"price\":\"1234567890.123456789\"}", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("{\"id\":\"abc\",\"price\":\"1\"}")]
    [InlineData("{\"id\":\"99999999999999999999\",\"price\":\"1\"}")]
    [InlineData("{\"id\":\"1\",\"price\":\"1234,56\"}")]
    public async Task ATextThatIsNotTheNumberIsABadRequest(string json)
    {
        var response = await Post(json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
