// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using NSail.Serialization;

namespace NSail.Types.Tests;

public class WireSchemaTests
{
    enum Kind
    {
        Far = 0,
        Near = 1
    }

    sealed class Payload
    {
        public required string Name { get; set; }

        [Required]
        public string? Code { get; set; }

        public int Retries { get; set; } = 3;

        public Kind Kind { get; set; }

        public string? Notes { get; set; }
    }

    [Fact]
    public void Members_carry_their_wire_names()
    {
        var properties = Properties(WireSchema.For(typeof(Payload)));

        Assert.Equal(["code", "kind", "name", "notes", "retries"], properties.Select(p => p.Key).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Enums_are_listed_by_name()
    {
        var kind = Properties(WireSchema.For(typeof(Payload)))["kind"]!;

        Assert.Equal(["Far", "Near"], kind["enum"]!.AsArray().Select(name => name!.GetValue<string>()));
    }

    [Fact]
    public void Required_lists_the_required_modifier_and_the_attribute_alike()
    {
        var required = WireSchema.For(typeof(Payload))["required"]!.AsArray().Select(name => name!.GetValue<string>());

        Assert.Equal(["code", "name"], required.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void A_non_nullable_reference_is_not_widened_to_null()
    {
        var name = Properties(WireSchema.For(typeof(Payload)))["name"]!;

        Assert.Equal("string", name["type"]!.GetValue<string>());
    }

    // The Web defaults read a number from a string too; an author given that choice sends
    // strings, and the schema is for authors, not for the reader's tolerance.
    [Fact]
    public void A_number_is_a_number_not_a_string()
    {
        var retries = Properties(WireSchema.For(typeof(Payload)))["retries"]!;

        Assert.Equal("integer", retries["type"]!.GetValue<string>());
    }

    [Fact]
    public void A_nullable_member_admits_null()
    {
        var notes = Properties(WireSchema.For(typeof(Payload)))["notes"]!;

        Assert.Equal(["string", "null"], notes["type"]!.AsArray().Select(type => type!.GetValue<string>()));
    }

    static JsonObject Properties(JsonNode schema)
    {
        return schema["properties"]!.AsObject();
    }
}
