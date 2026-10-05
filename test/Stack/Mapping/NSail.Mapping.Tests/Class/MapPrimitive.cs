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
    public void map_nullable_same_base()
    {
        Assert.Equal(0, Mappings.Map(new Nullables { NullableToValue = null }, new Converted()).NullableToValue);
        Assert.Equal(1, Mappings.Map(new Nullables { NullableToValue = 1 }, new Converted()).NullableToValue);
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
    }

    [MapFrom(typeof(Strings))]
    public sealed class Values
    {
        public int Number { get; set; }

        public string? Text { get; set; }

        public bool Flag { get; set; }
    }

    public sealed class Nullables
    {
        public int? NullableToValue { get; set; }

        public int ValueToNullable { get; set; }

        public int? NullableToNullable { get; set; }

        public string? StringToNullable { get; set; }

        public int? NullableToString { get; set; }
    }

    [MapFrom(typeof(Nullables))]
    public sealed class Converted
    {
        public int NullableToValue { get; set; }

        public int? ValueToNullable { get; set; }

        public int? NullableToNullable { get; set; }

        public int? StringToNullable { get; set; }

        public string? NullableToString { get; set; }
    }
}
