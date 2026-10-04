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
}
