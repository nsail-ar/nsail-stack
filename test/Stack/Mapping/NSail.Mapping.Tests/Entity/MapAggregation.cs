// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Mapping.Tests.Fixtures;

namespace NSail.Mapping.Tests.Entity;

// Ported from Detached.Mappers.Tests/Entity/MapAggregation.cs: a collection of entities the
// owner does not hold.
public sealed class MapAggregation
{
    [Fact]
    public void map_associated_collection()
    {
        var target = new TargetEntity
        {
            Id = 1,
            Name = "target_root",
            AssociatedList =
            [
                new TargetAssociatedItem { Id = 1, Name = "Item 1" },
                new TargetAssociatedItem { Id = 2, Name = "Item 2" },
                new TargetAssociatedItem { Id = 3, Name = "Item 3" },
            ],
        };

        var source = new SourceEntity
        {
            Id = 1,
            Name = "target_root",
            AssociatedList =
            [
                new SourceAssociatedItem { Id = 2, Name = "Item 2" },
                new SourceAssociatedItem { Id = 4, Name = "Item 4" },
            ],
        };

        var context = new RecordingContext();

        var mapped = Mappings.Map(source, target, context);

        Assert.Equal(2, mapped.AssociatedList.Count);
        Assert.True(context.Verify<TargetAssociatedItem>(4, out var action));
        Assert.Equal(MapAction.Attach, action);

        // What the collection stops naming leaves it and nothing more: an aggregation never
        // deletes the row it pointed at.
        Assert.False(context.Verify<TargetAssociatedItem>(1, out _));
        Assert.False(context.Verify<TargetAssociatedItem>(3, out _));
    }

    [MapFrom(typeof(SourceEntity))]
    [Entity]
    public sealed class TargetEntity
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public List<TargetAssociatedItem> AssociatedList { get; set; } = [];
    }

    [Entity]
    public sealed class TargetAssociatedItem
    {
        public int Id { get; set; }

        public string? Name { get; set; }
    }

    public sealed class SourceEntity
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public List<SourceAssociatedItem>? AssociatedList { get; set; }
    }

    public sealed class SourceAssociatedItem
    {
        public int Id { get; set; }

        public string? Name { get; set; }
    }
}
