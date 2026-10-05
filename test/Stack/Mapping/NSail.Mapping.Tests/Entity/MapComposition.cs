// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Mapping.Tests.Fixtures;

namespace NSail.Mapping.Tests.Entity;

// Ported from Detached.Mappers.Tests/Entity/MapComposition.cs.
public sealed class MapComposition
{
    [Fact]
    public void map_composition_collection()
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
                new SourceCompositionItem { Id = 2, Name = "Item 2" },
                new SourceCompositionItem { Id = 4, Name = "Item 4" },
            ],
        };

        var context = new RecordingContext();

        var mapped = Mappings.Map(source, target, context);

        Assert.Equal(2, mapped.CompositionList.Count);

        Assert.True(context.Verify<TargetCompositionItem>(1, out var item1));
        Assert.Equal(MapAction.Delete, item1);

        Assert.True(context.Verify<TargetCompositionItem>(2, out var item2));
        Assert.Equal(MapAction.Update, item2);

        Assert.True(context.Verify<TargetCompositionItem>(3, out var item3));
        Assert.Equal(MapAction.Delete, item3);

        Assert.True(context.Verify<TargetCompositionItem>(4, out var item4));
        Assert.Equal(MapAction.Create, item4);
    }

    // Detached records the keyless creation under the key's default value (0); here a source
    // that names no row has no key at all, so the creation is recorded under none.
    [Fact]
    public void map_composition_collection_null_keys()
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

        var source = new SourceEntityStringKey
        {
            Id = "1",
            Name = "target_root",
            CompositionList =
            [
                new SourceCompositionItemStringKey { Id = "2", Name = "Item 2" },
                new SourceCompositionItemStringKey { Name = "Item 4" },
            ],
        };

        var context = new RecordingContext();

        var mapped = Mappings.Map(source, target, context);

        Assert.Equal(2, mapped.CompositionList.Count);

        Assert.True(context.Verify<TargetCompositionItem>(1, out var item1));
        Assert.Equal(MapAction.Delete, item1);

        Assert.True(context.Verify<TargetCompositionItem>(2, out var item2));
        Assert.Equal(MapAction.Update, item2);

        Assert.True(context.Verify<TargetCompositionItem>(3, out var item3));
        Assert.Equal(MapAction.Delete, item3);

        Assert.True(context.Verify<TargetCompositionItem>(null!, out var item4));
        Assert.Equal(MapAction.Create, item4);
    }

    [MapFrom(typeof(SourceEntity))]
    [MapFrom(typeof(SourceEntityStringKey))]
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

    public sealed class SourceEntityStringKey
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public List<SourceCompositionItemStringKey>? CompositionList { get; set; }
    }

    public sealed class SourceCompositionItemStringKey
    {
        public string? Id { get; set; }

        public string? Name { get; set; }
    }
}
