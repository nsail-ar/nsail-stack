// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using NSail.Mapping.Annotations;
using NSail.Problems;

namespace NSail.Data.Tests.Mapping;

// Ported from Detached.Mappers.EntityFramework.Tests/DTOs/DtoToEntityTests.cs. Detached's
// MapAsync upserts by default; here Upsert says so, and Map refuses a row that is not there.
public sealed class DtoToEntityTests
{
    [Fact]
    public async Task map_dto_to_entity()
    {
        await using var store = await MapperStore<EntityTestDbContext>.Create();

        store.Db.Roles.Add(new Role { Id = 1, Name = "admin" });
        store.Db.Roles.Add(new Role { Id = 2, Name = "user" });
        store.Db.UserTypes.Add(new UserType { Id = 1, Name = "system" });
        await store.Db.SaveChangesAsync();

        await store.Mapper.Upsert<UserDto, User>(new UserDto
        {
            Id = 1,
            Name = "cr",
            Profile = new UserProfileDto { FirstName = "chris", LastName = "redfield" },
            Addresses = [new AddressDto { Street = "rc", Number = "123" }],
            Roles = [new RoleDto { Id = 1 }, new RoleDto { Id = 2 }],
            UserType = new UserTypeDto { Id = 1 },
        });

        await store.Db.SaveChangesAsync();

        await using var read = store.Fresh();

        var user = await read.Users.Where(u => u.Id == 1)
            .Include(u => u.Roles)
            .Include(u => u.Addresses)
            .Include(u => u.Profile)
            .Include(u => u.UserType)
            .FirstAsync();

        Assert.Equal("cr", user.Name);
        Assert.Equal("chris", user.Profile!.FirstName);
        Assert.Equal("redfield", user.Profile.LastName);
        Assert.Equal("rc", Assert.Single(user.Addresses!).Street);
        Assert.Equal("123", user.Addresses![0].Number);
        Assert.Equal(2, user.Roles!.Count);
        Assert.Contains(user.Roles, r => r.Id == 1);
        Assert.Contains(user.Roles, r => r.Id == 2);
        Assert.Equal(1, user.UserType!.Id);

        // Pointing at a role must not be able to rewrite it: the DTO named no Name.
        Assert.Equal("admin", read.Roles.Single(r => r.Id == 1).Name);
    }

    [Fact]
    public async Task map_dto_to_entity_notfound()
    {
        await using var store = await MapperStore<EntityTestDbContext>.Create();

        var refused = await Assert.ThrowsAsync<BusinessException>(() => store.Mapper.Map<UserDto, User>(new UserDto { Id = 1, Name = "cr" }));

        Assert.Equal(BusinessProblem.NotFoundCode, refused.Code);
    }

    // A reference to a row the store does not have is refused, never created on the way.
    [Fact]
    public async Task a_reference_to_a_missing_row_is_not_found()
    {
        await using var store = await MapperStore<EntityTestDbContext>.Create();

        await Assert.ThrowsAsync<BusinessException>(() => store.Mapper.Upsert<UserDto, User>(new UserDto { Id = 1, Name = "cr", UserType = new UserTypeDto { Id = 9 } }));
    }

    [MapFrom(typeof(UserDto))]
    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public virtual int Id { get; set; }

        public virtual string? Name { get; set; }

        public virtual DateTime DateOfBirth { get; set; }

        public virtual List<Role>? Roles { get; set; }

        [Composition]
        public virtual List<Address>? Addresses { get; set; }

        public virtual UserType? UserType { get; set; }

        [Composition]
        public virtual UserProfile? Profile { get; set; }
    }

    public class UserDto
    {
        public virtual int Id { get; set; }

        public virtual string? Name { get; set; }

        public virtual List<RoleDto>? Roles { get; set; }

        public virtual List<AddressDto>? Addresses { get; set; }

        public virtual UserTypeDto? UserType { get; set; }

        public virtual UserProfileDto? Profile { get; set; }
    }

    public class UserProfile
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public virtual int Id { get; set; }

        public virtual string? FirstName { get; set; }

        public virtual string? LastName { get; set; }
    }

    public class UserProfileDto
    {
        public virtual int Id { get; set; }

        public virtual string? FirstName { get; set; }

        public virtual string? LastName { get; set; }
    }

    public class Role
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public virtual int Id { get; set; }

        public virtual string? Name { get; set; }

        public virtual List<User>? Users { get; set; }
    }

    public class RoleDto
    {
        public virtual int Id { get; set; }

        public virtual string? Name { get; set; }
    }

    public class UserRole
    {
        public virtual int UserId { get; set; }

        public virtual User? User { get; set; }

        public virtual int RoleId { get; set; }

        public virtual Role? Role { get; set; }
    }

    public class UserType
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public virtual int Id { get; set; }

        public virtual string? Name { get; set; }

        public virtual List<User>? Users { get; set; }
    }

    public class UserTypeDto
    {
        public virtual int Id { get; set; }

        public virtual string? Name { get; set; }
    }

    public class Address
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public virtual int Id { get; set; }

        public virtual string? Street { get; set; }

        public virtual string? Number { get; set; }
    }

    public class AddressDto
    {
        public virtual int Id { get; set; }

        public virtual string? Street { get; set; }

        public virtual string? Number { get; set; }
    }

    public class EntityTestDbContext(DbContextOptions<EntityTestDbContext> options) : DbContext(options)
    {
        public DbSet<User> Users { get; set; } = null!;

        public DbSet<Role> Roles { get; set; } = null!;

        public DbSet<UserType> UserTypes { get; set; } = null!;

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
}
