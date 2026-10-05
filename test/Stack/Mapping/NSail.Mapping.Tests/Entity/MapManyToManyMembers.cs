// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Mapping.Tests.Fixtures;

namespace NSail.Mapping.Tests.Entity;

// Ported from Detached.Mappers.Tests/Entity/MapManyToManyMembers.cs. The two many-to-many
// tests beside it in Detached (MapManyToMany.cs) are disabled there and are not ported.
public sealed class MapManyToManyMembers
{
    [Fact]
    public void MapMultipleManyToMany()
    {
        var source = new EntityParent { Id = 1, Name = "ParentA", ListChildrenA = [new EntityChild { Id = 1, Name = "ChildA" }] };
        var target = new EntityParent { Id = 1 };

        Mappings.Map(source, target, new MapContext());

        Assert.Single(target.ListChildrenA!);
        Assert.Null(target.ListChildrenA![0].ListParentsB);
        Assert.Null(target.ListChildrenA[0].ListParentsC);
    }

    [MapFrom(typeof(EntityParent))]
    [Entity]
    public sealed class EntityParent
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        [Aggregation]
        public List<EntityChild>? ListChildrenA { get; set; }

        [Aggregation]
        public List<EntityChild>? ListChildrenB { get; set; }

        [Aggregation]
        public List<EntityChild>? ListChildrenC { get; set; }
    }

    [Entity]
    public sealed class EntityChild
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public EntityParent? BackReference { get; set; }

        [Aggregation]
        public List<EntityParent>? ListParentsA { get; set; }

        [Aggregation]
        public List<EntityParent>? ListParentsB { get; set; }

        [Aggregation]
        public List<EntityParent>? ListParentsC { get; set; }
    }
}
