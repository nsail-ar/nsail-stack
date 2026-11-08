// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.Json;
using NSail.Serialization;

namespace NSail.Types.Tests;

public class JsonOptionsTests
{
    enum Kind
    {
        Far = 0,
        Near = 1
    }

    sealed class Payload
    {
        public Kind Kind { get; set; }
    }

    [Fact]
    public void Wire_writes_enums_as_names()
    {
        var json = JsonSerializer.Serialize(new Payload { Kind = Kind.Near }, JsonOptions.Wire);

        Assert.Equal("{\"kind\":\"Near\"}", json);
    }

    [Fact]
    public void Wire_reads_enum_names()
    {
        var payload = JsonSerializer.Deserialize<Payload>("{\"kind\":\"Far\"}", JsonOptions.Wire);

        Assert.Equal(Kind.Far, payload!.Kind);
    }

    [Fact]
    public void Wire_still_reads_enum_numbers()
    {
        var payload = JsonSerializer.Deserialize<Payload>("{\"kind\":1}", JsonOptions.Wire);

        Assert.Equal(Kind.Near, payload!.Kind);
    }

    sealed class Exact
    {
        public long Id { get; set; }
        public ulong Big { get; set; }
        public decimal Price { get; set; }
        public long? Maybe { get; set; }
        public int Count { get; set; }
        public double Ratio { get; set; }
    }

    [Fact]
    public void Wire_writes_long_ulong_and_decimal_as_strings_and_leaves_the_rest_numbers()
    {
        var json = JsonSerializer.Serialize(new Exact { Id = 9007199254740993, Big = ulong.MaxValue, Price = 1234567890.123456789m, Count = 3, Ratio = 0.5 }, JsonOptions.Wire);

        Assert.Equal("{\"id\":\"9007199254740993\",\"big\":\"18446744073709551615\",\"price\":\"1234567890.123456789\",\"maybe\":null,\"count\":3,\"ratio\":0.5}", json);
    }

    [Fact]
    public void Wire_reads_those_types_from_strings_and_from_numbers()
    {
        var text = JsonSerializer.Deserialize<Exact>("{\"id\":\"9007199254740993\",\"price\":\"0.1234567890123456789\",\"maybe\":\"7\"}", JsonOptions.Wire)!;
        var number = JsonSerializer.Deserialize<Exact>("{\"id\":42,\"price\":1.5}", JsonOptions.Wire)!;

        Assert.Equal(9007199254740993, text.Id);
        Assert.Equal(0.1234567890123456789m, text.Price);
        Assert.Equal(7, text.Maybe);
        Assert.Equal(42, number.Id);
        Assert.Equal(1.5m, number.Price);
    }
}
