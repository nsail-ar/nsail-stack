// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSail.Data;

namespace NSail.BaseServices.WebApi.Tests;

/// <summary>What the install's record about a tenant does to its requests: a suspended customer
/// is answered the suspended page and reaches nothing else, and the answer follows the row
/// rather than a memo of it.</summary>
public sealed class TenantStateTests
{
    const string Slug = "lumina";

    [Fact]
    public async Task A_tenant_with_no_state_row_is_served()
    {
        await using var host = await TenancyHost.Start(TenancyMode.SingleDb);

        using var response = await host.Client.SendAsync(host.Get("/where", Slug));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // A word this Stack does not know is not a suspension: the column is written by a program
    // outside the app, and a typo in that hand must not take a paying customer down.
    [Fact]
    public async Task A_state_this_stack_cannot_read_is_not_a_suspension()
    {
        await using var host = await TenancyHost.Start(TenancyMode.SingleDb);

        await State(host, Slug, "paused-ish");

        using var response = await host.Client.SendAsync(host.Get("/where", Slug));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("es", "Cuenta suspendida")]
    [InlineData("en", "Account suspended")]
    public async Task A_suspended_tenants_browser_gets_the_suspended_page(string language, string title)
    {
        await using var host = await TenancyHost.Start(TenancyMode.SingleDb, language: language);

        await State(host, Slug, TenantState.Suspended);

        var request = host.Get("/where", Slug);

        request.Headers.Add("Accept", "text/html");

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());

        var document = await response.Content.ReadAsStringAsync();

        Assert.Contains(title, document, StringComparison.Ordinal);
        Assert.Contains($"<html lang=\"{language}\"", document, StringComparison.Ordinal);

        // The page carries no asset, and that is the design: every asset of this install is an
        // endpoint behind this gate, so a document that linked one would be refused its own
        // stylesheet and arrive bare.
        Assert.DoesNotContain("<link", document, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", document, StringComparison.OrdinalIgnoreCase);
    }

    // The other caller: a generated client, a fetch, curl. Accept alone tells them apart, and
    // the machine gets the machine's answer — the same negotiation the error handler makes.
    [Fact]
    public async Task A_suspended_tenants_client_gets_the_problem()
    {
        await using var host = await TenancyHost.Start(TenancyMode.SingleDb);

        await State(host, Slug, TenantState.Suspended);

        using var response = await host.Client.SendAsync(host.Get("/where", Slug));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Suspended", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    // The gate stands where the install that does not exist is refused: ahead of
    // authentication, so a suspended tenant cannot even mint a ticket.
    [Fact]
    public async Task A_suspended_tenant_cannot_sign_in()
    {
        await using var host = await TenancyHost.Start(TenancyMode.SingleDb);

        await State(host, Slug, TenantState.Suspended);

        using var response = await host.Client.SendAsync(host.Get("/sign-in?stamp=true", Slug));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task A_suspended_tenant_does_not_suspend_its_neighbour()
    {
        await using var host = await TenancyHost.Start(TenancyMode.SingleDb);

        await State(host, Slug, TenantState.Suspended);

        using var refused = await host.Client.SendAsync(host.Get("/where", Slug));
        using var served = await host.Client.SendAsync(host.Get("/where", "aurora"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, refused.StatusCode);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
    }

    // Nothing is remembered between requests, and this is why: the row is written from outside
    // this process, so a memo would go on refusing a customer the install has already restored.
    [Fact]
    public async Task A_reactivated_tenant_is_served_again()
    {
        await using var host = await TenancyHost.Start(TenancyMode.SingleDb);

        await State(host, Slug, TenantState.Suspended);

        using (var refused = await host.Client.SendAsync(host.Get("/where", Slug)))
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, refused.StatusCode);
        }

        await State(host, Slug, TenantState.Active);

        using var served = await host.Client.SendAsync(host.Get("/where", Slug));

        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
    }

    [Fact]
    public async Task The_plan_a_tenant_is_on_is_read_in_that_tenants_scope()
    {
        await using var host = await TenancyHost.Start(TenancyMode.SingleDb);

        await State(host, Slug, TenantState.Active, plan: "Pro");

        await using var scope = host.Services.CreateAsyncScope();

        scope.ServiceProvider.GetRequiredService<ResolvedTenancyProvider>().Enter(Tenant.For(Slug));

        var state = await scope.ServiceProvider.GetRequiredService<TenantStates>().Current();

        Assert.Equal("Pro", state?.Plan);
    }

    // What the runner does, in the only shape a test can do it: the row is written by a program
    // outside this app, by the tenant's own key and through no scope of the app's.
    static async Task State(TenancyHost host, string tenant, string status, string? plan = null)
    {
        await using var scope = host.Services.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<DbContext>();
        var key = TenantKey.For(tenant);

        if (await db.Set<TenantState>().FirstOrDefaultAsync(row => row.TenantId == key) is { } existing)
        {
            existing.Status = status;
            existing.Plan = plan;
        }
        else
        {
            db.Add(new TenantState { TenantId = key, Status = status, Plan = plan });
        }

        await db.SaveChangesAsync();
    }
}
