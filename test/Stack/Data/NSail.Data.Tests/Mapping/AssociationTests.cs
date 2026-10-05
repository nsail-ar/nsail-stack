// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using NSail.Mapping.Annotations;

namespace NSail.Data.Tests.Mapping;

// Ported from Detached.Mappers.EntityFramework.Tests/Association/*.cs and FullGraphTests.cs.
// The IList and no-setter cases map anonymous objects in Detached; here the source is a named
// input carrying the same members.
public sealed class AssociationTests
{
    [Fact]
    public async Task map_association()
    {
        await using var store = await Seeded();

        await store.Mapper.Upsert<EditUserRoles, User>(new EditUserRoles { Id = 1, Roles = [new Role { Id = 1 }] });
        await store.Db.SaveChangesAsync();

        await using var read = store.Fresh();

        var saved = await read.Users.Where(u => u.Id == 1).Include(u => u.Roles).Include(u => u.Addresses).Include(u => u.Profile).Include(u => u.UserType).FirstAsync();

        Assert.Equal("test user", saved.Name);
        Assert.Equal("test", saved.Profile!.FirstName);
        Assert.Equal("original street", Assert.Single(saved.Addresses!).Street);
        Assert.Equal(1, Assert.Single(saved.Roles!).Id);
        Assert.Equal(1, saved.UserType!.Id);

        // The role it stopped naming left the collection and stayed in the store.
        Assert.True(await read.Roles.AnyAsync(r => r.Id == 2));
    }

    [Fact]
    public async Task map_input()
    {
        await using var store = await Seeded();

        var saved = await store.Db.Users.Where(u => u.Id == 1).Include(u => u.Roles).Include(u => u.Addresses).Include(u => u.Profile).Include(u => u.UserType).FirstAsync();

        await store.Mapper.Upsert<EditUserName, User>(new EditUserName { Id = 1, Name = "edited name" });

        Assert.Equal("edited name", saved.Name);
        Assert.Equal("test", saved.Profile!.FirstName);
        Assert.Equal("user", saved.Profile.LastName);
        Assert.Equal("original street", Assert.Single(saved.Addresses!).Street);
        Assert.Equal(2, saved.Roles!.Count);
        Assert.Equal(1, saved.UserType!.Id);
    }

    [Fact]
    public async Task map_association_ilist()
    {
        await using var store = await MapperStore<AddressBookContext>.Create();

        var result = await store.Mapper.Upsert<PersonInput, ListPerson>(new PersonInput { Id = 1, Name = "test user", Addresses = [new AddressInput { Id = 1, Line1 = "123 Main St." }] });

        Assert.Equal("test user", result.Name);
        Assert.Equal(1, result.Addresses![0].Id);
        Assert.Equal("123 Main St.", result.Addresses[0].Line1);
    }

    [Fact]
    public async Task map_association_nosetter()
    {
        await using var store = await MapperStore<AddressBookContext>.Create();

        var result = await store.Mapper.Upsert<PersonInput, FixedPerson>(new PersonInput { Id = 1, Name = "test user", Addresses = [new AddressInput { Id = 1, Line1 = "123 Main St." }] });

        Assert.Equal("test user", result.Name);
        Assert.Equal(1, result.Addresses[0].Id);
        Assert.Equal("123 Main St.", result.Addresses[0].Line1);
    }

    static async Task<MapperStore<UsersContext>> Seeded()
    {
        var store = await MapperStore<UsersContext>.Create();

        store.Db.Roles.Add(new Role { Id = 1, Name = "admin" });
        store.Db.Roles.Add(new Role { Id = 2, Name = "user" });
        store.Db.UserTypes.Add(new UserType { Id = 1, Name = "system" });
        await store.Db.SaveChangesAsync();

        store.Db.Users.Add(new User
        {
            Id = 1,
            Name = "test user",
            Roles = [store.Db.Find<Role>(1)!, store.Db.Find<Role>(2)!],
            Addresses = [new Address { Id = 1, Street = "original street", Number = "123" }],
            Profile = new UserProfile { Id = 1, FirstName = "test", LastName = "user" },
            UserType = store.Db.Find<UserType>(1),
        });

        await store.Db.SaveChangesAsync();

        return store;
    }

    public class EditUserRoles
    {
        public int Id { get; set; }

        public List<Role>? Roles { get; set; }
    }

    public class EditUserName
    {
        public int Id { get; set; }

        public string? Name { get; set; }
    }

    [MapFrom(typeof(EditUserRoles))]
    [MapFrom(typeof(EditUserName))]
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

    public class UserProfile
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
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

    public class Address
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public virtual int Id { get; set; }

        public virtual string? Street { get; set; }

        public virtual string? Number { get; set; }
    }

    public class UsersContext(DbContextOptions<UsersContext> options) : DbContext(options)
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

    public class PersonInput
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public AddressInput[]? Addresses { get; set; }
    }

    public class AddressInput
    {
        public int Id { get; set; }

        public string? Line1 { get; set; }
    }

    [MapFrom(typeof(PersonInput))]
    public class ListPerson
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        [Composition]
        public IList<ListAddress>? Addresses { get; set; }
    }

    public class ListAddress
    {
        public int Id { get; set; }

        public string? Line1 { get; set; }

        public string? Line2 { get; set; }
    }

    [MapFrom(typeof(PersonInput))]
    public class FixedPerson
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        [Composition]
        public List<FixedAddress> Addresses { get; } = [];
    }

    public class FixedAddress
    {
        public int Id { get; set; }

        public string? Line1 { get; set; }

        public string? Line2 { get; set; }
    }

    public class AddressBookContext(DbContextOptions<AddressBookContext> options) : DbContext(options)
    {
        public DbSet<ListPerson> ListPeople { get; set; } = null!;

        public DbSet<FixedPerson> FixedPeople { get; set; } = null!;
    }
}
