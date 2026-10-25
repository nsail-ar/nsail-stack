// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Security.Tests;

internal sealed class Declaring : IFeatureContributor
{
    readonly FeatureItem[] _items;

    public Declaring(params FeatureItem[] items)
    {
        _items = items;
    }

    public IReadOnlyList<FeatureItem> GetItems()
    {
        return _items;
    }
}

/// <summary>The Stack holds no list of keys: it gathers what the composed modules declare beside
/// the code that enforces each one. What the catalog adds to the bag is the DECLARED default — a
/// key nobody has written about a tenant still has an answer, and that answer is the key's
/// owner's to state rather than the gate's to assume.</summary>
public sealed class FeatureCatalogTests
{
    static FeatureItem Flag(string key, bool enabled = true)
    {
        return new FeatureItem { Key = key, Kind = FeatureKind.Flag, Enabled = enabled };
    }

    static FeatureItem Limit(string key, decimal? limit = null)
    {
        return new FeatureItem { Key = key, Kind = FeatureKind.Limit, Limit = limit };
    }

    [Fact]
    public void A_catalog_with_nothing_composed_declares_nothing()
    {
        var features = new Features([]);

        Assert.Empty(features.All);
        Assert.Null(features.Of("whatsapp"));
    }

    [Fact]
    public void Every_composed_module_contributes_its_own_keys()
    {
        var features = new Features([new Declaring(Flag("whatsapp")), new Declaring(Limit("organizations.max"))]);

        Assert.Equal(2, features.All.Count);
        Assert.Equal(FeatureKind.Flag, features.Of("whatsapp")!.Kind);
        Assert.Equal(FeatureKind.Limit, features.Of("organizations.max")!.Kind);
    }

    /// <summary>Two modules may both own a road and neither can be made to know the other
    /// exists, so a key declared twice is one key and the gathering does not depend on the order
    /// a host composed in.</summary>
    [Fact]
    public void A_key_declared_twice_is_one_key()
    {
        var features = new Features([new Declaring(Flag("whatsapp")), new Declaring(Flag("WhatsApp", enabled: false))]);

        Assert.Single(features.All);
        Assert.True(features.IsEnabled("whatsapp", Entitlements.Unrestricted));
    }

    [Fact]
    public void A_key_the_bag_says_nothing_about_takes_its_declared_default()
    {
        var features = new Features([new Declaring(Flag("arca", enabled: false), Limit("organizations.max", 3))]);

        Assert.False(features.IsEnabled("arca", Entitlements.Unrestricted));
        Assert.Equal(3m, features.Limit("organizations.max", Entitlements.Unrestricted));
    }

    [Fact]
    public void What_the_bag_states_outranks_the_declared_default()
    {
        var features = new Features([new Declaring(Flag("arca", enabled: false), Limit("organizations.max", 3))]);
        var bag = Entitlements.Read("""{"arca": true, "organizations.max": 10}""");

        Assert.True(features.IsEnabled("arca", bag));
        Assert.Equal(10m, features.Limit("organizations.max", bag));
    }

    /// <summary>A mark naming a key nobody declared is a defect the composed products' own sweep
    /// catches — not a door to close on a running install.</summary>
    [Fact]
    public void A_key_nobody_declared_is_open_and_has_no_ceiling()
    {
        var features = new Features([new Declaring(Flag("whatsapp"))]);

        Assert.True(features.IsEnabled("invented", Entitlements.Unrestricted));
        Assert.Null(features.Limit("invented", Entitlements.Unrestricted));
    }

    /// <summary>The provider is what a caller asks, and it resolves the default itself: a key
    /// declared off with nothing written about this tenant is off.</summary>
    [Fact]
    public async Task The_provider_answers_the_whole_question_default_included()
    {
        var provider = new FeatureProvider(new Features([new Declaring(Flag("arca", enabled: false), Limit("users.max", 5))]));

        Assert.False(await provider.IsEnabled("arca"));
        Assert.False(provider.Assumes("arca"));
        Assert.True(await provider.IsEnabled("whatsapp"));
        Assert.Equal(5m, await provider.Limit("users.max"));
    }

    /// <summary>The client cannot reach the column, so it is handed what the server read. Until
    /// something hands it over the base is permissive, which is the client's standing rule: the
    /// server gate is the real check.</summary>
    [Fact]
    public async Task A_bag_handed_to_the_client_is_what_it_then_answers()
    {
        var provider = new FeatureProvider(new Features([new Declaring(Flag("whatsapp"))]));

        Assert.True(await provider.IsEnabled("whatsapp"));

        provider.Assign(Entitlements.Read("""{"whatsapp": false, "organizations.max": 3}"""));

        Assert.False(await provider.IsEnabled("whatsapp"));
        Assert.False(provider.Assumes("whatsapp"));
        Assert.Equal(3m, await provider.Limit("organizations.max"));
    }

    [Fact]
    public async Task A_provider_with_no_catalog_reads_the_bag_alone()
    {
        var provider = new FeatureProvider();

        Assert.True(await provider.IsEnabled("arca"));
        Assert.Null(await provider.Limit("users.max"));
    }
}
