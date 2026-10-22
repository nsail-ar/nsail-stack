// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using NSail.Mapping.Annotations;
using NSail.Problems;

namespace NSail.Data.Tests.Mapping;

// Ported from Detached.Mappers.EntityFramework.Tests/Keys/*.cs: a source that names no key,
// or a nullable one, is a create.
public sealed class KeyTests
{
    [Fact]
    public async Task map_entity_nokey_dto()
    {
        await using var store = await MapperStore<KeylessContext>.Create();

        store.Db.Roles.Add(new Role { Id = 1, Name = "admin" });
        store.Db.Roles.Add(new Role { Id = 2, Name = "user" });
        await store.Db.SaveChangesAsync();

        await store.Mapper.Upsert<NoKeyUserDto, User>(new NoKeyUserDto
        {
            Name = "nokeyuser",
            Roles = [new Role { Id = 1 }, new Role { Id = 2 }],
        });

        await store.Db.SaveChangesAsync();

        await using var read = store.Fresh();

        var persisted = await read.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Name == "nokeyuser");

        Assert.NotNull(persisted);
        Assert.Equal("nokeyuser", persisted.Name);
        Assert.Equal(2, persisted.Roles!.Count);
        Assert.Equal("admin", read.Roles.Single(r => r.Id == 1).Name);
    }

    [Fact]
    public async Task map_entity_nullable_key()
    {
        await using var store = await MapperStore<NullableKeyContext>.Create();

        var customer = await store.Mapper.Upsert<CustomerDto, Customer>(new CustomerDto { Id = Guid.NewGuid(), Name = "new customer" });

        Assert.Equal("new customer", customer.Name);
    }

    [Fact]
    public async Task map_dto_nullable_key()
    {
        await using var store = await MapperStore<NullableKeyContext>.Create();

        var customer = await store.Mapper.Map<CustomerDto, Customer>(new CustomerDto { Id = null, Name = "new customer" });

        Assert.Equal("new customer", customer.Name);
    }

    // A nullable key member naming a real id that is not there is still NotFound — the type
    // being Guid? decides what null means, never whether a value in hand gets checked.
    [Fact]
    public async Task map_dto_nullable_key_realid_notfound()
    {
        await using var store = await MapperStore<NullableKeyContext>.Create();

        var id = Guid.NewGuid();

        var refused = await Assert.ThrowsAsync<BusinessException>(
            () => store.Mapper.Map<CustomerDto, Customer>(new CustomerDto { Id = id, Name = "ghost" }));

        Assert.Equal(BusinessProblem.NotFoundCode, refused.Code);
        Assert.Equal(id.ToString(), refused.Issues.Single().Arguments!["id"]);
        Assert.Empty(store.Db.Customers);
    }

    // A nullable key member carrying its own type's default is a value, not an absence: the
    // caller distinguished "no id" (null) from "id Guid.Empty" already, and Map must too.
    [Fact]
    public async Task map_dto_nullable_key_default_notfound()
    {
        await using var store = await MapperStore<NullableKeyContext>.Create();

        var refused = await Assert.ThrowsAsync<BusinessException>(
            () => store.Mapper.Map<CustomerDto, Customer>(new CustomerDto { Id = Guid.Empty, Name = "ghost" }));

        Assert.Equal(BusinessProblem.NotFoundCode, refused.Code);
        Assert.Equal(Guid.Empty.ToString(), refused.Issues.Single().Arguments!["id"]);
        Assert.Empty(store.Db.Customers);
    }

    public class NoKeyUserDto
    {
        public string? Name { get; set; }

        public List<Role>? Roles { get; set; }
    }

    [MapFrom(typeof(NoKeyUserDto))]
    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        public string? Name { get; set; }

        public DateTime DateOfBirth { get; set; }

        public List<Role>? Roles { get; set; }
    }

    public class Role
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        public string? Name { get; set; }

        public List<User>? Users { get; set; }
    }

    public class UserRole
    {
        public int UserId { get; set; }

        public User? User { get; set; }

        public int RoleId { get; set; }

        public Role? Role { get; set; }
    }

    public class KeylessContext(DbContextOptions<KeylessContext> options) : DbContext(options)
    {
        public DbSet<User> Users { get; set; } = null!;

        public DbSet<Role> Roles { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .HasMany(u => u.Roles)
                .WithMany(r => r.Users)
                .UsingEntity<UserRole>(
                    ur => ur.HasOne(u => u.Role).WithMany().HasForeignKey(u => u.RoleId),
                    ur => ur.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId))
                .HasKey(ur => new { ur.UserId, ur.RoleId });
        }
    }

    [MapFrom(typeof(CustomerDto))]
    public class Customer
    {
        public Guid Id { get; set; }

        public string? Name { get; set; }
    }

    public class CustomerDto
    {
        public Guid? Id { get; set; }

        public string? Name { get; set; }
    }

    public class NullableKeyContext(DbContextOptions<NullableKeyContext> options) : DbContext(options)
    {
        public DbSet<Customer> Customers { get; set; } = null!;
    }
}
