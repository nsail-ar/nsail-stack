// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using NSail.Mapping.Annotations;

namespace NSail.Data.Tests.Mapping;

// Ported from Detached.Mappers.EntityFramework.Tests/Batch/BatchTests.cs.
public sealed class BatchTests
{
    [Fact]
    public async Task map_many()
    {
        await using var store = await MapperStore<BatchContext>.Create();

        store.Db.Users.Add(new User { Id = 1, Name = "usr1" });
        store.Db.Users.Add(new User { Id = 2, Name = "usr2" });
        await store.Db.SaveChangesAsync();

        var entities = await store.Mapper.Upsert<UserDto, User>([
            new UserDto { Id = 1, Name = "usr1_modified" },
            new UserDto { Id = 2, Name = "usr2_modified" },
        ]);

        await store.Db.SaveChangesAsync();

        Assert.Contains(entities, e => e.Name == "usr1_modified");
        Assert.Contains(entities, e => e.Name == "usr2_modified");

        await using var read = store.Fresh();

        var persisted = await read.Users.ToListAsync();

        Assert.Contains(persisted, e => e.Name == "usr1_modified");
        Assert.Contains(persisted, e => e.Name == "usr2_modified");
    }

    [MapFrom(typeof(UserDto))]
    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        public string? Name { get; set; }

        public DateTime DateOfBirth { get; set; }
    }

    public class UserDto
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public DateTime DateOfBirth { get; set; }
    }

    public class BatchContext(DbContextOptions<BatchContext> options) : DbContext(options)
    {
        public DbSet<User> Users { get; set; } = null!;
    }
}
