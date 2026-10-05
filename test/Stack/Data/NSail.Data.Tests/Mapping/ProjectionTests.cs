// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using NSail.Mapping.Annotations;

namespace NSail.Data.Tests.Mapping;

// Ported from Detached.Mappers.EntityFramework.Tests/Binding/BindForeingKeyToDtoTests.cs, plus
// the shapes a projection must still translate to SQL: a null reference and a null-guarded
// collection.
public sealed class ProjectionTests
{
    [Fact]
    public async Task bind_fk_to_dto()
    {
        await using var store = await MapperStore<ProjectionContext>.Create();

        store.Db.Parents.Add(new ParentEntity
        {
            Id = 1,
            Name = "Parent 1",
            Child = new ChildEntity { Id = 1, Name = "Child 1" },
            Children = [new ChildEntity { Id = 2, Name = "Child 2" }, new ChildEntity { Id = 3, Name = "Child 3" }],
        });
        await store.Db.SaveChangesAsync();

        await using var read = store.Fresh();

        var result = await MapperStore<ProjectionContext>.MapperOver(read).Project<ParentEntity, ParentDto>(read.Parents).FirstAsync();

        Assert.Equal(1, result.ChildId);
        Assert.Equal([2, 3], result.ChildIds!.Order());
    }

    [Fact]
    public async Task project_graph_in_store()
    {
        await using var store = await MapperStore<ProjectionContext>.Create();

        store.Db.Parents.Add(new ParentEntity
        {
            Id = 1,
            Name = "Parent 1",
            Children = [new ChildEntity { Id = 2, Name = "Child 2" }],
        });
        await store.Db.SaveChangesAsync();

        await using var read = store.Fresh();

        var result = await MapperStore<ProjectionContext>.MapperOver(read).Project<ParentEntity, ParentRow>(read.Parents).SingleAsync();

        Assert.Equal("Parent 1", result.Name);
        Assert.Null(result.Child);
        Assert.Equal("Child 2", Assert.Single(result.Children!).Name);
    }

    public class ParentDto
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public int ChildId { get; set; }

        public List<int>? ChildIds { get; set; }
    }

    public class ParentRow
    {
        public string? Name { get; set; }

        public ChildRow? Child { get; set; }

        public List<ChildRow>? Children { get; set; }
    }

    public class ChildRow
    {
        public string? Name { get; set; }
    }

    [MapTo(typeof(ParentDto))]
    [MapTo(typeof(ParentRow))]
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

    public class ProjectionContext(DbContextOptions<ProjectionContext> options) : DbContext(options)
    {
        public DbSet<ParentEntity> Parents { get; set; } = null!;

        public DbSet<ChildEntity> Children { get; set; } = null!;
    }
}
