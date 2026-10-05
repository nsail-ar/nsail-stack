// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Mapping.Tests.Fixtures;

namespace NSail.Mapping.Tests.Entity;

// Ported from Detached.Mappers.Tests/Entity/MapOwned.cs: a single composed entity.
public sealed class MapOwned
{
    [Fact]
    public void create_composition_entity_when_target_is_null()
    {
        var source = new SourceDto { Id = 1, Name = "dto", Composition = new CompositionDto { Id = 2, Name = "composition_dto" } };
        var target = new TargetEntity { Id = 1, Name = "entity", ExtraProperty = "extra_prop_not_mapped", Composition = null };
        var context = new RecordingContext();

        var mapped = Mappings.Map(source, target, context);

        Assert.Same(target, mapped);
        Assert.Equal(1, mapped.Id);
        Assert.Equal("dto", mapped.Name);
        Assert.Equal("extra_prop_not_mapped", mapped.ExtraProperty);
        Assert.NotNull(mapped.Composition);
        Assert.Equal(2, mapped.Composition!.Id);
        Assert.Equal("composition_dto", mapped.Composition.Name);
        Assert.True(context.Verify<CompositionEntity>(2, out var action));
        Assert.Equal(MapAction.Create, action);
    }

    [Fact]
    public void update_composition_entity_when_key_matches()
    {
        var source = new SourceDto { Id = 1, Name = "dto", Composition = new CompositionDto { Id = 2, Name = "composition_dto" } };
        var composition = new CompositionEntity { Id = 2, Name = "composition_entity" };
        var target = new TargetEntity { Id = 1, Name = "entity", ExtraProperty = "extra_prop_not_mapped", Composition = composition };
        var context = new RecordingContext();

        var mapped = Mappings.Map(source, target, context);

        Assert.Same(target, mapped);
        Assert.Equal("dto", mapped.Name);
        Assert.Equal("extra_prop_not_mapped", mapped.ExtraProperty);
        Assert.Same(composition, mapped.Composition);
        Assert.Equal(2, mapped.Composition!.Id);
        Assert.Equal("composition_dto", mapped.Composition.Name);
        Assert.True(context.Verify<CompositionEntity>(2, out var action));
        Assert.Equal(MapAction.Update, action);
    }

    [Fact]
    public void replace_composition_entity_when_key_not_matching()
    {
        var source = new SourceDto { Id = 1, Name = "dto", Composition = new CompositionDto { Id = 2, Name = "composition_dto" } };
        var composition = new CompositionEntity { Id = 3, Name = "composition_entity" };
        var target = new TargetEntity { Id = 1, Name = "entity", ExtraProperty = "extra_prop_not_mapped", Composition = composition };
        var context = new RecordingContext();

        var mapped = Mappings.Map(source, target, context);

        Assert.Same(target, mapped);
        Assert.NotSame(composition, mapped.Composition);
        Assert.Equal(2, mapped.Composition!.Id);
        Assert.Equal("composition_dto", mapped.Composition.Name);
        Assert.True(context.Verify<CompositionEntity>(2, out var created));
        Assert.Equal(MapAction.Create, created);
        Assert.True(context.Verify<CompositionEntity>(3, out var deleted));
        Assert.Equal(MapAction.Delete, deleted);
    }

    [Fact]
    public void delete_composition_entity_when_source_is_null()
    {
        var source = new SourceDto { Id = 1, Name = "dto", Composition = null };
        var target = new TargetEntity { Id = 1, Name = "entity", ExtraProperty = "extra_prop_not_mapped", Composition = new CompositionEntity { Id = 2, Name = "composition_entity" } };
        var context = new RecordingContext();

        var mapped = Mappings.Map(source, target, context);

        Assert.Same(target, mapped);
        Assert.Null(mapped.Composition);
        Assert.True(context.Verify<CompositionEntity>(2, out var action));
        Assert.Equal(MapAction.Delete, action);
    }

    [MapFrom(typeof(SourceDto))]
    [Entity]
    public sealed class TargetEntity
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public string? ExtraProperty { get; set; }

        [Composition]
        public CompositionEntity? Composition { get; set; }
    }

    [Entity]
    public sealed class CompositionEntity
    {
        public int Id { get; set; }

        public string? Name { get; set; }
    }

    public sealed class SourceDto
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public CompositionDto? Composition { get; set; }
    }

    public sealed class CompositionDto
    {
        public int Id { get; set; }

        public string? Name { get; set; }
    }
}
