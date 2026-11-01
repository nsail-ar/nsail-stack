// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSail.Data;
using NSail.Messaging.WebApi;

namespace NSail.BaseServices.WebApi.Tests;

/// <summary>nsail#3002: a tenant's request budget, read through <see cref="RequestBudgetProvider"/>
/// and refused past it with 429 and <c>Retry-After</c>. The provider is stubbed because the figure
/// is the product's to read — the Stack names no entitlement key; the clock is stubbed so the
/// seconds left are an exact figure; and the tenant is stubbed off a header because what is under
/// test is the partition, not how a tenant is resolved. The pipeline is the real one, as in
/// <see cref="ThrottleTests"/>.</summary>
public sealed class RequestBudgetTests
{
    const int PerMinute = 3;

    [Fact]
    public async Task Past_the_budget_a_request_gets_429_with_the_seconds_left_in_the_window()
    {
        await using var host = await BudgetHost.Start(PerMinute);

        for (var attempt = 0; attempt < PerMinute; attempt++)
        {
            using var allowed = await host.Get("acme");

            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        host.Clock.Advance(TimeSpan.FromSeconds(20));

        using var refused = await host.Get("acme");

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal("TooManyRequests", await Code(refused));
        Assert.Equal(TimeSpan.FromSeconds(40), refused.Headers.RetryAfter?.Delta);
    }

    // And when those seconds are up, the window has reopened.
    [Fact]
    public async Task Once_the_window_closes_the_budget_is_whole_again()
    {
        await using var host = await BudgetHost.Start(PerMinute);

        for (var attempt = 0; attempt <= PerMinute; attempt++)
        {
            using var spent = await host.Get("acme");
        }

        host.Clock.Advance(TimeSpan.FromMinutes(1));

        using var reopened = await host.Get("acme");

        Assert.Equal(HttpStatusCode.OK, reopened.StatusCode);
    }

    // One tenant's flood is spent out of its own share and nobody else's.
    [Fact]
    public async Task Each_tenant_spends_a_budget_of_its_own()
    {
        await using var host = await BudgetHost.Start(PerMinute);

        for (var attempt = 0; attempt < PerMinute; attempt++)
        {
            using var spent = await host.Get("acme");
        }

        using var refused = await host.Get("acme");
        using var other = await host.Get("globex");

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
    }

    // The base provider has no figure, and neither has a figure that would refuse everything:
    // suspending is the state row's word, never a number's.
    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public async Task A_tenant_with_no_budget_is_never_refused(int? figure)
    {
        await using var host = await BudgetHost.Start(figure);

        for (var attempt = 0; attempt < PerMinute * 3; attempt++)
        {
            using var response = await host.Get("acme");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    static async Task<string> Code(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.GetProperty("code").GetString() ?? string.Empty;
    }
}

sealed class StubBudget : RequestBudgetProvider
{
    readonly int? _perMinute;

    public StubBudget(int? perMinute)
    {
        _perMinute = perMinute;
    }

    public override Task<int?> PerMinute(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_perMinute);
    }
}

sealed class HeaderTenancy : TenancyProvider
{
    public const string Header = "X-Probe-Tenant";

    readonly IHttpContextAccessor _http;

    public HeaderTenancy(IHttpContextAccessor http)
        : base(new TenancyOptions())
    {
        _http = http;
    }

    public override Tenant Current
    {
        get { return Tenant.For(_http.HttpContext!.Request.Headers[Header].ToString()); }
    }
}

sealed class BudgetHost : IAsyncDisposable
{
    readonly WebApplication _app;
    readonly HttpClient _client;

    BudgetHost(WebApplication app, HttpClient client, TestClock clock)
    {
        _app = app;
        _client = client;
        Clock = clock;
    }

    public TestClock Clock { get; }

    public static async Task<BudgetHost> Start(int? perMinute)
    {
        var builder = WebApplication.CreateBuilder();
        var clock = new TestClock();

        builder.WebHost.UseTestServer();

        builder.AddBaseWebApi();

        builder.Services.AddSingleton<TimeProvider>(clock);
        builder.Services.AddAuthentication();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<RequestBudgetProvider>(_ => new StubBudget(perMinute));
        builder.Services.AddScoped<TenancyProvider, HeaderTenancy>();
        builder.Services.AddTransient<IEndpointEntry>(_ => new Probe(typeof(Uncapped), Uncapped.Path));

        var app = builder.Build();

        app.UseBaseWebApi();

        await app.StartAsync();

        return new BudgetHost(app, app.GetTestClient(), clock);
    }

    public Task<HttpResponseMessage> Get(string tenant)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"https://localhost/{Uncapped.Path}"));

        request.Headers.Add(HeaderTenancy.Header, tenant);

        return _client.SendAsync(request);
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();

        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
