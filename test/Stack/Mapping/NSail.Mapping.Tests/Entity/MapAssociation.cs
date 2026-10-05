// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Mapping.Tests.Fixtures;

namespace NSail.Mapping.Tests.Entity;

// Ported from Detached.Mappers.Tests/Entity/MapAssociation.cs: a member whose type is an
// entity, unannotated, is an aggregation.
public sealed class MapAssociation
{
    [Fact]
    public void load_associated_when_key_doesnt_match()
    {
        var associated = new TargetAssociated { Id = 1, Name = "target_associated" };
        var target = new TargetEntity { Id = 1, Name = "target", Associated = associated };
        var source = new SourceEntity { Id = 1, Name = "source", Associated = new SourceAssociated { Id = 2, Name = "source_associated" } };
        var context = new RecordingContext();

        var mapped = Mappings.Map(source, target, context);

        Assert.NotNull(mapped.Associated);
        Assert.Equal(2, mapped.Associated!.Id);
        Assert.NotSame(associated, mapped.Associated);
        Assert.False(context.Verify<TargetAssociated>(1, out _));
        Assert.True(context.Verify<TargetAssociated>(2, out var action));
        Assert.Equal(MapAction.Attach, action);
    }

    [Fact]
    public void load_associated_when_target_is_null()
    {
        var target = new TargetEntity { Id = 1, Name = "target", Associated = null };
        var source = new SourceEntity { Id = 1, Name = "source", Associated = new SourceAssociated { Id = 2, Name = "source_associated" } };
        var context = new RecordingContext();

        var mapped = Mappings.Map(source, target, context);

        Assert.NotNull(mapped.Associated);
        Assert.Equal(2, mapped.Associated!.Id);
        Assert.False(context.Verify<TargetAssociated>(1, out _));
        Assert.True(context.Verify<TargetAssociated>(2, out var action));
        Assert.Equal(MapAction.Attach, action);
    }

    [Fact]
    public void dont_load_associated_when_key_matches()
    {
        var associated = new TargetAssociated { Id = 1, Name = "target_associated" };
        var target = new TargetEntity { Id = 1, Name = "target", Flag = true, Associated = associated };
        var source = new SourceEntity { Id = 1, Name = "source", Flag = true, Associated = new SourceAssociated { Id = 1, Name = "source_associated" } };
        var context = new RecordingContext();

        var mapped = Mappings.Map(source, target, context);

        Assert.Same(associated, mapped.Associated);
        Assert.Equal("target_associated", mapped.Associated!.Name);
        Assert.False(context.Verify<TargetAssociated>(1, out _));
    }

    [MapFrom(typeof(SourceEntity))]
    [Entity]
    public sealed class TargetEntity
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public bool Flag { get; set; }

        public TargetAssociated? Associated { get; set; }
    }

    [Entity]
    public sealed class TargetAssociated
    {
        public int Id { get; set; }

        public string? Name { get; set; }
    }

    public sealed class SourceEntity
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public bool Flag { get; set; }

        public SourceAssociated? Associated { get; set; }
    }

    public sealed class SourceAssociated
    {
        public int Id { get; set; }

        public string? Name { get; set; }
    }
}
