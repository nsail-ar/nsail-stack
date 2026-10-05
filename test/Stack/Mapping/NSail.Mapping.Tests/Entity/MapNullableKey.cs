// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Mapping.Tests.Fixtures;

namespace NSail.Mapping.Tests.Entity;

// Ported from Detached.Mappers.Tests/Entity/MapNullableKey.cs: a target with no key yet takes
// the source's.
public sealed class MapNullableKey
{
    [Fact]
    public void map_nullable_key()
    {
        var target = new NullableKeyEntity();
        var source = new NullableKeyDto { Id = Guid.NewGuid(), Name = "the dto" };

        Mappings.Map(source, target, new MapContext());

        Assert.Equal(source.Id, target.Id);
        Assert.Equal(source.Name, target.Name);
    }

    [MapFrom(typeof(NullableKeyDto))]
    public sealed class NullableKeyEntity
    {
        public Guid? Id { get; set; }

        public string? Name { get; set; }
    }

    public sealed class NullableKeyDto
    {
        public Guid? Id { get; set; }

        public string? Name { get; set; }
    }
}
