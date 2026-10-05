// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Mapping.Tests.Fixtures;

namespace NSail.Mapping.Tests.Projection;

// Ported from Detached.Mappers.Tests/Binding/BindComplex.cs.
public sealed class BindComplex
{
    [Fact]
    public void bind_complex()
    {
        var dto = Mappings.Projected<RootEntity, RootDto>()(new RootEntity { Id = 1, Name = "Entity" });

        Assert.Equal(1, dto.Id);
        Assert.Equal("Entity", dto.Name);
        Assert.Null(dto.Reference);
    }

    [Fact]
    public void bind_complex_nested()
    {
        var dto = Mappings.Projected<RootEntity, RootDto>()(new RootEntity
        {
            Id = 1,
            Name = "Entity",
            Reference = new ReferencedEntity { Id = 2, Name = "SubEntity" },
        });

        Assert.Equal(1, dto.Id);
        Assert.Equal("Entity", dto.Name);
        Assert.NotNull(dto.Reference);
        Assert.Equal(2, dto.Reference.Id);
        Assert.Equal("SubEntity", dto.Reference.Name);
    }

    [Fact]
    public void bind_renamed_and_converted()
    {
        var dto = Mappings.Projected<RootEntity, RootDto>()(new RootEntity { Id = 1, Name = "Entity", Code = 7, Score = 3 });

        Assert.Equal("Entity", dto.Title);
        Assert.Equal(7L, dto.Code);
        Assert.Equal(3, dto.Score);
    }

    public class RootDto
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public string? Title { get; set; }

        public long Code { get; set; }

        public int Score { get; set; }

        public ReferencedDto? Reference { get; set; }
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

        [MapTo(typeof(RootDto), nameof(RootDto.Title))]
        public string? Caption
        {
            get { return Name; }
        }

        public string? Name { get; set; }

        public int Code { get; set; }

        public int? Score { get; set; }

        public ReferencedEntity? Reference { get; set; }
    }

    public class ReferencedEntity
    {
        public int Id { get; set; }

        public string? Name { get; set; }
    }
}
