// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Security.Tests;

/// <summary>The bag is written by a program outside this codebase, in plain SQL, and the fleet's
/// rows are empty the day it ships — so every reading that is not a flag the bag positively
/// closed has to come back permissive. A restrictive answer to a typo, to an empty column or to
/// a key this version has never heard of would cut paying customers off on deploy day.</summary>
public sealed class EntitlementsTests
{
    [Fact]
    public void No_bag_leaves_every_feature_on_and_no_ceiling()
    {
        foreach (var json in new string?[] { null, "", "   " })
        {
            var entitlements = Entitlements.Read(json);

            Assert.True(entitlements.IsEnabled("whatsapp"));
            Assert.Null(entitlements.Limit("organizations.max"));
            Assert.Empty(entitlements.Keys);
        }
    }

    [Fact]
    public void An_empty_bag_reads_the_same_as_no_bag()
    {
        var entitlements = Entitlements.Read("{}");

        Assert.True(entitlements.IsEnabled("whatsapp"));
        Assert.Null(entitlements.Limit("organizations.max"));
    }

    [Fact]
    public void A_flag_is_off_only_where_the_bag_says_so()
    {
        var entitlements = Entitlements.Read("""{"whatsapp": false, "arca": true}""");

        Assert.False(entitlements.IsEnabled("whatsapp"));
        Assert.True(entitlements.IsEnabled("arca"));
        Assert.True(entitlements.IsEnabled("assistant"));
    }

    [Fact]
    public void A_number_is_the_ceiling_and_a_key_with_none_is_unlimited()
    {
        var entitlements = Entitlements.Read("""{"organizations.max": 3, "users.max": null}""");

        Assert.Equal(3m, entitlements.Limit("organizations.max"));
        Assert.Null(entitlements.Limit("users.max"));
        Assert.Null(entitlements.Limit("storage.bytes"));
    }

    /// <summary>The keys are written by hand into a json column: a case the writer typed
    /// differently is the same key, exactly as the row's Status is compared.</summary>
    [Fact]
    public void A_key_is_matched_whatever_case_it_was_written_in()
    {
        var entitlements = Entitlements.Read("""{"WhatsApp": false, "Organizations.Max": 3}""");

        Assert.False(entitlements.IsEnabled("whatsapp"));
        Assert.Equal(3m, entitlements.Limit("organizations.max"));
    }

    /// <summary>A kind the key was not declared as is not an answer of the other kind: a flag
    /// asked for a number has no ceiling, and a ceiling asked as a flag is on.</summary>
    [Fact]
    public void A_key_of_the_other_kind_answers_its_own_permissive_default()
    {
        var entitlements = Entitlements.Read("""{"whatsapp": false, "organizations.max": 3}""");

        Assert.Null(entitlements.Limit("whatsapp"));
        Assert.True(entitlements.IsEnabled("organizations.max"));
    }

    [Fact]
    public void A_bag_no_parser_can_read_leaves_the_tenant_where_it_found_it()
    {
        foreach (var json in new[] { "not json at all", """{"whatsapp": fals""", "[1, 2, 3]", "\"whatsapp\"", "7" })
        {
            var entitlements = Entitlements.Read(json);

            Assert.True(entitlements.IsEnabled("whatsapp"));
            Assert.Null(entitlements.Limit("organizations.max"));
        }
    }

    /// <summary>A value of a kind the Stack carries neither of — an object, a list, text — is
    /// skipped and the keys beside it still stand: one bad entry is not a bad bag.</summary>
    [Fact]
    public void A_value_of_no_kind_the_Stack_knows_is_skipped_key_by_key()
    {
        var entitlements = Entitlements.Read(
            """{"whatsapp": false, "branding": {"mark": true}, "plans": ["max"], "tier": "max"}""");

        Assert.False(entitlements.IsEnabled("whatsapp"));
        Assert.True(entitlements.IsEnabled("branding"));
        Assert.True(entitlements.IsEnabled("tier"));
        Assert.Equal(["whatsapp"], entitlements.Keys);
    }

    [Fact]
    public void A_bag_handed_over_as_values_reads_as_one_read_from_the_column()
    {
        var entitlements = Entitlements.Of(
            new Dictionary<string, bool> { ["whatsapp"] = false },
            new Dictionary<string, decimal> { ["organizations.max"] = 3 });

        Assert.False(entitlements.IsEnabled("whatsapp"));
        Assert.Equal(3m, entitlements.Limit("organizations.max"));
        Assert.Same(Entitlements.Unrestricted, Entitlements.Of(null, null));
    }

    [Fact]
    public void The_bag_states_what_it_carries_for_whoever_has_to_send_it_on()
    {
        var entitlements = Entitlements.Read("""{"whatsapp": false, "organizations.max": 3}""");

        Assert.Equal(false, entitlements.Flags["whatsapp"]);
        Assert.Equal(3m, entitlements.Limits["organizations.max"]);
        Assert.Equal(["whatsapp", "organizations.max"], entitlements.Keys);
    }
}
