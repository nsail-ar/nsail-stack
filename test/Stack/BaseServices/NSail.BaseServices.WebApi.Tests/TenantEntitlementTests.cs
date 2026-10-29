// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSail.Data;

namespace NSail.BaseServices.WebApi.Tests;

/// <summary>What the install's record about a tenant says the tenant is ENTITLED to, read on a
/// real host behind the real pipeline: the bag is a jsonb column a program outside the app
/// writes, the gate reads it through the composed <c>FeatureProvider</c>, and every answer the
/// bag does not positively close is the permissive one — which is the state of the whole fleet
/// the day the column ships.</summary>
public sealed class TenantEntitlementTests
{
    const string Slug = "lumina";

    [Fact]
    public async Task A_tenant_with_no_row_has_every_feature_and_no_ceiling()
    {
        await using var host = await TenancyHost.Start(TenancyMode.SingleDb);

        var bag = await Bag(host);

        Assert.True(bag.WhatsApp);
        Assert.Null(bag.Organizations);
    }

    [Fact]
    public async Task A_row_carrying_no_bag_reads_the_same_as_no_row()
    {
        await using var host = await TenancyHost.Start(TenancyMode.SingleDb);

        await State(host, new TenantState { Plan = "starter" });

        var bag = await Bag(host);

        Assert.True(bag.WhatsApp);
        Assert.Null(bag.Organizations);
    }

    [Fact]
    public async Task A_flag_the_bag_closes_comes_back_closed_and_the_ceiling_beside_it_stands()
    {
        await using var host = await TenancyHost.Start(TenancyMode.SingleDb);

        await State(host, new TenantState
        {
            Plan = "starter",
            Entitlements = """{"whatsapp": false, "organizations.max": 3}""",
            EntitlementsUpdatedAt = DateTime.UtcNow,
        });

        var bag = await Bag(host);

        Assert.False(bag.WhatsApp);
        Assert.Equal(3m, bag.Organizations);
    }

    /// <summary>A key this version has never heard of changes nothing, which is what lets the
    /// fleet's writer run ahead of the cells it writes to.</summary>
    [Fact]
    public async Task A_key_the_install_does_not_know_is_carried_and_ignored()
    {
        await using var host = await TenancyHost.Start(TenancyMode.SingleDb);

        await State(host, new TenantState { Entitlements = """{"sold.next.year": false}""" });

        var bag = await Bag(host);

        Assert.True(bag.WhatsApp);
        Assert.Null(bag.Organizations);
    }

    /// <summary>Under a mode that resolves no tenant there is no row to read and no
    /// <c>TenantStates</c> registered at all — the provider still resolves and still answers,
    /// because an install with one customer must behave as it did before the bag existed.</summary>
    [Fact]
    public async Task A_mode_that_resolves_no_tenant_answers_the_permissive_bag()
    {
        await using var host = await TenancyHost.Start(TenancyMode.None);

        using var response = await host.Client.SendAsync(host.Get("/entitlements", tenant: null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var bag = (await response.Content.ReadFromJsonAsync<Bag>())!;

        Assert.True(bag.WhatsApp);
        Assert.Null(bag.Organizations);
    }

    static async Task<Bag> Bag(TenancyHost host)
    {
        using var response = await host.Client.SendAsync(host.Get("/entitlements", Slug));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<Bag>())!;
    }

    // What whoever operates the fleet does, as TenantStateTests does it: the row is written by
    // the tenant's own key and replaced whole, because the columns are that writer's contract.
    static async Task State(TenancyHost host, TenantState row)
    {
        await using var scope = host.Services.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<DbContext>();
        var key = TenantKey.For(Slug);

        if (await db.Set<TenantState>().FirstOrDefaultAsync(existing => existing.TenantId == key) is { } held)
        {
            db.Remove(held);

            await db.SaveChangesAsync();
        }

        row.TenantId = key;

        db.Add(row);

        await db.SaveChangesAsync();
    }
}
