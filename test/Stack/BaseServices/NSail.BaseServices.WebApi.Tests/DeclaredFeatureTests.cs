// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSail.Data;
using NSail.Security;

namespace NSail.BaseServices.WebApi.Tests;

/// <summary>What the install publishes ABOUT ITSELF, on a real host over the slot's own Postgres:
/// the entitlement keys the build composed into it declares, written to its own table at startup
/// so whoever operates the fleet types against the version actually running here. The mirror of
/// <see cref="TenantEntitlementTests"/>, which reads the row the operator writes back.</summary>
public sealed class DeclaredFeatureTests
{
    [Fact]
    public async Task An_install_publishes_every_key_its_build_declares_with_its_kind_and_default()
    {
        await using var host = await TenancyHost.Start(TenancyMode.SingleDb, features: typeof(TwoSold));

        var published = await Published(host);

        Assert.Equal(["organizations.max", "whatsapp"], published.Select(row => row.Key));

        var flag = published.Single(row => row.Key == "whatsapp");

        Assert.Equal(nameof(FeatureKind.Flag), flag.Kind);
        Assert.False(flag.Enabled);
        Assert.Null(flag.Limit);

        var ceiling = published.Single(row => row.Key == "organizations.max");

        Assert.Equal(nameof(FeatureKind.Limit), ceiling.Kind);
        Assert.Equal(3m, ceiling.Limit);
    }

    /// <summary>A build that owns no key publishes nothing, which is the honest answer and not an
    /// omission: the same table, empty, is what an install running a version older than it has.
    /// Whoever reads a cell cannot tell the two apart and owes neither an entry.</summary>
    [Fact]
    public async Task A_build_that_declares_nothing_publishes_nothing()
    {
        await using var host = await TenancyHost.Start(TenancyMode.SingleDb);

        Assert.Empty(await Published(host));
    }

    // The same build again, which is every deploy and every restart: the table is amended and
    // never appended to, so a key is published once however often the install comes up.
    [Fact]
    public async Task A_second_start_of_the_same_build_rewrites_rather_than_duplicates()
    {
        await using var host = await TenancyHost.Start(TenancyMode.SingleDb, features: typeof(TwoSold));

        await using var redeployed = await TenancyHost.Redeploy(host, features: typeof(TwoSold));

        Assert.Equal(["organizations.max", "whatsapp"], (await Published(redeployed)).Select(row => row.Key));
    }

    // And a deploy that moved a declaration moves the row with it, in both directions: what the
    // table says is what THIS build declares, never the union of every build that ever ran here.
    [Fact]
    public async Task A_deploy_that_changes_a_declaration_changes_what_is_published()
    {
        await using var host = await TenancyHost.Start(TenancyMode.SingleDb, features: typeof(TwoSold));

        await using var redeployed = await TenancyHost.Redeploy(host, features: typeof(OneSold));

        var published = await Published(redeployed);

        var flag = Assert.Single(published);

        Assert.Equal("whatsapp", flag.Key);
        Assert.True(flag.Enabled);
    }

    /// <summary>The keys are the install's and not a tenant's, so a mode that resolves no tenant
    /// publishes exactly the same list — an install with one customer is still a version somebody
    /// has to know what they can sell on.</summary>
    [Fact]
    public async Task A_mode_that_resolves_no_tenant_publishes_the_same_keys()
    {
        await using var host = await TenancyHost.Start(TenancyMode.None, features: typeof(TwoSold));

        Assert.Equal(["organizations.max", "whatsapp"], (await Published(host)).Select(row => row.Key));
    }

    // Read as the fleet's own program reads it: off the table, by the key, with nothing of the
    // app's catalog in the answer.
    static async Task<IReadOnlyList<DeclaredFeature>> Published(TenancyHost host)
    {
        await using var scope = host.Services.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<DbContext>();

        return await db.Set<DeclaredFeature>()
            .OrderBy(row => row.Key)
            .ToListAsync();
    }
}

// What a kit that owns two keys declares beside the code that enforces them, with the two kinds
// and two non-permissive defaults — stated rather than left at the permissive ones, because the
// subject here is the carriage of a declaration and a default nobody wrote reads the same as a
// column that lost it.
internal sealed class TwoSold : IFeatureContributor
{
    public IReadOnlyList<FeatureItem> GetItems()
    {
        return
        [
            new FeatureItem { Key = "whatsapp", Kind = FeatureKind.Flag, Enabled = false },
            new FeatureItem { Key = "organizations.max", Kind = FeatureKind.Limit, Limit = 3m },
        ];
    }
}

// The next build of the same install: one key gone and the other's default moved.
internal sealed class OneSold : IFeatureContributor
{
    public IReadOnlyList<FeatureItem> GetItems()
    {
        return [new FeatureItem { Key = "whatsapp", Kind = FeatureKind.Flag, Enabled = true }];
    }
}
