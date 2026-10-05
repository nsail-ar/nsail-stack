// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using NSail.Mapping.Tests.Fixtures;

namespace NSail.Mapping.Tests.Entity;

// Ported from Detached.Mappers.Tests/Entity/MapNoKey.cs: a source with no key, onto an entity
// in hand.
public sealed class MapNoKey
{
    [Fact]
    public void map_dto_without_key()
    {
        var entity = new KeyedEntity { Id = 1, Name = "the entity" };

        Mappings.Map(new NoKeyDto { Name = "the dto" }, entity, new MapContext());

        Assert.Equal("the dto", entity.Name);
        Assert.Equal(1, entity.Id);
    }

    [MapFrom(typeof(NoKeyDto))]
    [Entity]
    public sealed class KeyedEntity
    {
        [Key]
        public int Id { get; set; }

        public string? Name { get; set; }
    }

    public sealed class NoKeyDto
    {
        public string? Name { get; set; }
    }
}
