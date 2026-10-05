// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Mapping.Tests.Fixtures;

namespace NSail.Mapping.Tests.Class;

// A `required` member must be said at construction; the mapper says it and writes it from the
// source right after, and a projection leaves one it has no source for at its default.
public sealed class MapRequired
{
    [Fact]
    public void map_onto_required_members()
    {
        var result = Mappings.Map<SaleDto, Sale>(new SaleDto { Id = 1, Name = "first" });

        Assert.Equal(1, result.Id);
        Assert.Equal("first", result.Name);
    }

    [Fact]
    public void project_into_required_members()
    {
        var row = Mappings.Projected<Sale, SaleRow>()(new Sale { Id = 2, Name = "second" });

        Assert.Equal("second", row.Name);
        Assert.Null(row.Code);
    }

    public sealed class SaleDto
    {
        public int Id { get; set; }

        public string? Name { get; set; }
    }

    [MapFrom(typeof(SaleDto))]
    [MapTo(typeof(SaleRow))]
    public sealed class Sale
    {
        public int Id { get; set; }

        public required string Name { get; set; }
    }

    public sealed class SaleRow
    {
        public required string Name { get; set; }

        public required string? Code { get; set; }
    }
}
