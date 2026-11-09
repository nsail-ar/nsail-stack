// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Mapping.Tests.Fixtures;

namespace NSail.Mapping.Tests.Class;

// Ported from Detached.Mappers.Tests/Class/Primitive/MapPrimitive.cs and MapNullablePrimitives.cs.
// Detached maps a bare value at the root (Map<string, int>("1")); a generated mapper is declared
// on a type, so each conversion is a member of one.
public sealed class MapPrimitive
{
    [Fact]
    public void map_string_to_int()
    {
        Assert.Equal(1, Mappings.Map(new Strings { Number = "1" }, new Values()).Number);
    }

    [Fact]
    public void map_string_to_string()
    {
        Assert.Equal("1", Mappings.Map(new Strings { Text = "1" }, new Values()).Text);
    }

    [Fact]
    public void map_string_to_bool()
    {
        Assert.True(Mappings.Map(new Strings { Flag = "true" }, new Values()).Flag);
    }

    [Fact]
    public void map_narrowing_throws_instead_of_wrapping()
    {
        Assert.Equal(5, Mappings.Map(new Wide { Number = 5L, Money = 5m }, new Narrow()).Number);
        Assert.Throws<OverflowException>(() => Mappings.Map(new Wide { Number = long.MaxValue }, new Narrow()));
        Assert.Throws<OverflowException>(() => Mappings.Map(new Wide { Money = 1e12m }, new Narrow()));
    }

    [Fact]
    public void map_enum_narrowing_throws_instead_of_wrapping()
    {
        Assert.Equal(Small.B, Mappings.Map(new Numbered { Kind = 1 }, new Enumerated()).Kind);
        Assert.Throws<OverflowException>(() => Mappings.Map(new Numbered { Kind = 300 }, new Enumerated()));
    }

    [Fact]
    public void map_unparsable_text_is_an_input_problem()
    {
        var number = Assert.Throws<NSail.Problems.BusinessException>(() => Mappings.Map(new Strings { Number = "abc" }, new Values()));
        Assert.Equal("InvalidModel", number.Code);
        Assert.Equal("InvalidFormat", Assert.Single(number.Issues).Code);
        Assert.Equal("Number", Assert.Single(number.Issues).Source);
        Assert.Equal("Number", Assert.Single(number.Issues).Arguments!["field"]);

        Assert.Throws<NSail.Problems.BusinessException>(() => Mappings.Map(new Strings { Flag = "maybe" }, new Values()));
    }

    [Fact]
    public void map_unparsable_text_is_sourced_at_the_field_sent()
    {
        var problem = Assert.Throws<NSail.Problems.BusinessException>(() => Mappings.Map(new Strings { Quantity = "abc" }, new Values()));
        var issue = Assert.Single(problem.Issues);
        Assert.Equal("Quantity", issue.Source);
        Assert.Equal("Quantity", issue.Arguments!["field"]);
    }

    [Fact]
    public void map_nullable_same_base()
    {
        Assert.Equal(1, Mappings.Map(new Nullables { ValueToNullable = 1 }, new Converted()).ValueToNullable);
        Assert.Equal(1, Mappings.Map(new Nullables { NullableToNullable = 1 }, new Converted()).NullableToNullable);
    }

    [Fact]
    public void map_nullable_different_base()
    {
        Assert.Null(Mappings.Map(new Nullables { StringToNullable = null }, new Converted()).StringToNullable);
        Assert.Equal(1, Mappings.Map(new Nullables { StringToNullable = "1" }, new Converted()).StringToNullable);
        Assert.Null(Mappings.Map(new Nullables { NullableToString = null }, new Converted()).NullableToString);
        Assert.Equal("1", Mappings.Map(new Nullables { NullableToString = 1 }, new Converted()).NullableToString);
    }

    public sealed class Strings
    {
        public string? Number { get; set; }

        public string? Text { get; set; }

        public string? Flag { get; set; }

        public string? Quantity { get; set; }
    }

    [MapFrom(typeof(Strings))]
    public sealed class Values
    {
        public int Number { get; set; }

        public string? Text { get; set; }

        public bool Flag { get; set; }

        [MapFrom(typeof(Strings), nameof(Strings.Quantity))]
        public int Count { get; set; }
    }

    public sealed class Nullables
    {
        public int ValueToNullable { get; set; }

        public int? NullableToNullable { get; set; }

        public string? StringToNullable { get; set; }

        public int? NullableToString { get; set; }
    }

    [MapFrom(typeof(Nullables))]
    public sealed class Converted
    {
        public int? ValueToNullable { get; set; }

        public int? NullableToNullable { get; set; }

        public int? StringToNullable { get; set; }

        public string? NullableToString { get; set; }
    }

    public sealed class Wide
    {
        public long Number { get; set; }

        public decimal Money { get; set; }
    }

    [MapFrom(typeof(Wide))]
    public sealed class Narrow
    {
        public int Number { get; set; }

        public int Money { get; set; }
    }

    public enum Small : byte
    {
        A,
        B,
    }

    public sealed class Numbered
    {
        public int Kind { get; set; }
    }

    [MapFrom(typeof(Numbered))]
    public sealed class Enumerated
    {
        public Small Kind { get; set; }
    }
}
