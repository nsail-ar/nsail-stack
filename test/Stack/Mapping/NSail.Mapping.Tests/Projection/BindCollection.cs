// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Mapping.Tests.Fixtures;

namespace NSail.Mapping.Tests.Projection;

// Ported from Detached.Mappers.Tests/Binding/BindCollection.cs. A list of roots is the
// projection applied per item, as a query's Select does.
public sealed class BindCollection
{
    [Fact]
    public void bind_collection()
    {
        var project = Mappings.Projected<RootEntity, RootDto>();

        var dto = new List<RootEntity> { new() { Id = 1, Name = "Entity" } }.Select(project).ToList();

        Assert.Equal(1, dto[0].Id);
        Assert.Equal("Entity", dto[0].Name);
        Assert.Null(dto[0].Reference);
    }

    [Fact]
    public void bind_collection_property()
    {
        var dto = Mappings.Projected<RootEntity, RootDto>()(new RootEntity
        {
            Id = 1,
            Name = "Entity",
            Reference = [new ReferencedEntity { Id = 2, Name = "SubEntity" }],
            Tags = ["a", "b"],
        });

        Assert.Equal(1, dto.Id);
        Assert.Equal("Entity", dto.Name);
        Assert.NotNull(dto.Reference);
        Assert.Equal(2, dto.Reference[0].Id);
        Assert.Equal("SubEntity", dto.Reference[0].Name);
        Assert.Equal(["a", "b"], dto.Tags!);
    }

    public class RootDto
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public List<ReferencedDto>? Reference { get; set; }

        public string[]? Tags { get; set; }
    }

    public class ReferencedDto
    {
        public int Id { get; set; }

        public string? Name { get; set; }
    }

    [MapTo(typeof(RootDto))]
    public class RootEntity
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public List<ReferencedEntity>? Reference { get; set; }

        public List<string>? Tags { get; set; }
    }

    public class ReferencedEntity
    {
        public int Id { get; set; }

        public string? Name { get; set; }
    }
}
