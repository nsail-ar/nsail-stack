// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Mapping.Tests.Fixtures;

namespace NSail.Mapping.Tests.Entity;

// Ported from Detached.Mappers.Tests/Entity/MapParent.cs.
public sealed class MapParent
{
    [Fact]
    public void map_parent()
    {
        var target = new TargetEntity
        {
            Id = 1,
            Name = "target_root",
            CompositionList =
            [
                new TargetCompositionItem { Id = 1, Name = "Item 1" },
                new TargetCompositionItem { Id = 2, Name = "Item 2" },
                new TargetCompositionItem { Id = 3, Name = "Item 3" },
            ],
        };

        var source = new SourceEntity
        {
            Id = 1,
            Name = "target_root",
            CompositionList =
            [
                new SourceCompositionItem { Id = 1, Name = "Item 1" },
                new SourceCompositionItem { Id = 2, Name = "Item 2" },
                new SourceCompositionItem { Id = 3, Name = "Item 3" },
            ],
        };

        var mapped = Mappings.Map(source, target, new MapContext());

        Assert.Same(mapped, mapped.CompositionList[0].Parent);
        Assert.Same(mapped, mapped.CompositionList[1].Parent);
        Assert.Same(mapped, mapped.CompositionList[2].Parent);
    }

    [MapFrom(typeof(SourceEntity))]
    [Entity]
    public sealed class TargetEntity
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        [Composition]
        public List<TargetCompositionItem> CompositionList { get; set; } = [];
    }

    [Entity]
    public sealed class TargetCompositionItem
    {
        [Parent]
        public TargetEntity? Parent { get; set; }

        public int Id { get; set; }

        public string? Name { get; set; }
    }

    public sealed class SourceEntity
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public List<SourceCompositionItem>? CompositionList { get; set; }
    }

    public sealed class SourceCompositionItem
    {
        public int Id { get; set; }

        public string? Name { get; set; }
    }
}
