// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using NSail.Mapping.Annotations;

namespace NSail.Data.Tests.Mapping;

// Ported from Detached.Mappers.EntityFramework.Tests/ForeignKeys/*.cs: a reference written by
// its key alone, one (ChildId for Child) or many (ChildIds for Children).
public sealed class ForeignKeyTests
{
    [Fact]
    public async Task map_fk_to_entity()
    {
        await using var store = await MapperStore<ShortNameContext>.Create();

        store.Db.Children.Add(new ChildEntity { Name = "Child 1" });
        store.Db.Children.Add(new ChildEntity { Name = "Child 2" });
        await store.Db.SaveChangesAsync();

        var result = await store.Mapper.Upsert<ParentInput, ParentEntity>(new ParentInput { Id = 1, Name = "test user", ChildId = 2, ChildIds = [1, 2] });

        Assert.Equal(2, result.Child!.Id);
        Assert.Equal("Child 2", result.Child.Name);
        Assert.Equal(1, result.Children![0].Id);
        Assert.Equal("Child 1", result.Children[0].Name);
        Assert.Equal(2, result.Children[1].Id);
        Assert.Equal("Child 2", result.Children[1].Name);
    }

    [Fact]
    public async Task map_fk_longname_to_entity()
    {
        await using var store = await MapperStore<LongNameContext>.Create();

        store.Db.Children.Add(new LongChild { Name = "Child 1" });
        store.Db.Children.Add(new LongChild { Name = "Child 2" });
        await store.Db.SaveChangesAsync();

        var result = await store.Mapper.Upsert<LongParentInput, LongParent>(new LongParentInput { ParentId = 1, Name = "test user", ChildId = 2, ChildIds = [1, 2] });

        Assert.Equal(2, result.Child!.ChildId);
        Assert.Equal("Child 2", result.Child.Name);
        Assert.Equal(1, result.Children![0].ChildId);
        Assert.Equal("Child 1", result.Children[0].Name);
        Assert.Equal(2, result.Children[1].ChildId);
        Assert.Equal("Child 2", result.Children[1].Name);
    }

    public class ParentInput
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public int ChildId { get; set; }

        public int[]? ChildIds { get; set; }
    }

    [MapFrom(typeof(ParentInput))]
    public class ParentEntity
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public ChildEntity? Child { get; set; }

        public List<ChildEntity>? Children { get; set; }
    }

    public class ChildEntity
    {
        public int Id { get; set; }

        public string? Name { get; set; }
    }

    public class ShortNameContext(DbContextOptions<ShortNameContext> options) : DbContext(options)
    {
        public DbSet<ParentEntity> Parents { get; set; } = null!;

        public DbSet<ChildEntity> Children { get; set; } = null!;
    }

    public class LongParentInput
    {
        public int ParentId { get; set; }

        public string? Name { get; set; }

        public int ChildId { get; set; }

        public int[]? ChildIds { get; set; }
    }

    [MapFrom(typeof(LongParentInput))]
    public class LongParent
    {
        [Key]
        public int ParentId { get; set; }

        public string? Name { get; set; }

        public LongChild? Child { get; set; }

        public List<LongChild>? Children { get; set; }
    }

    public class LongChild
    {
        [Key]
        public int ChildId { get; set; }

        public string? Name { get; set; }
    }

    public class LongNameContext(DbContextOptions<LongNameContext> options) : DbContext(options)
    {
        public DbSet<LongParent> Parents { get; set; } = null!;

        public DbSet<LongChild> Children { get; set; } = null!;
    }
}
