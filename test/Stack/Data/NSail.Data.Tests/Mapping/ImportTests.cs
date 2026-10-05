// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NSail.Mapping.Annotations;

namespace NSail.Data.Tests.Mapping;

// Ported from Detached.Mappers.EntityFramework.Tests/Import/ImportJsonTests.cs. Detached reads
// the document as untyped JSON, where a property left out is not written; here the document is
// read into a type, and a member the type does not carry is the one left alone.
public sealed class ImportTests
{
    static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task map_json_string()
    {
        await using var store = await MapperStore<ImportContext>.Create();

        store.Db.Users.Add(new User { Id = 1, Name = "test user", DateOfBirth = new DateTime(1984, 07, 09) });
        await store.Db.SaveChangesAsync();
        store.Db.ChangeTracker.Clear();

        await store.Mapper.Import<UserName, User>(Json<UserName>("[ { 'id': 1, 'name': 'test user 2' } ]"));
        await store.Db.SaveChangesAsync();

        await using var read = store.Fresh();

        var persisted = await read.Users.SingleAsync(u => u.Id == 1);
        Assert.Equal("test user 2", persisted.Name);
        Assert.Equal(new DateTime(1984, 07, 09), persisted.DateOfBirth);
    }

    [Fact]
    public async Task map_json_string_associations_ordered()
    {
        await using var store = await MapperStore<ImportContext>.Create();

        await store.Mapper.Import<RoleRow, Role>(Json<RoleRow>("[ { 'id': 1, 'name': 'admin' }, { 'id': 2, 'name': 'power user' } ]"));
        await store.Mapper.Import<UserRow, User>(Json<UserRow>("[ { 'id': 1, 'name': 'test user', 'roles': [ { 'id': 1 }, { 'id': 2 } ] } ]"));
        await store.Db.SaveChangesAsync();

        await AssertUserWithRoles(store);
    }

    [Fact]
    public async Task map_json_string_associations_not_ordered()
    {
        await using var store = await MapperStore<ImportContext>.Create();

        await store.Mapper.Import<UserRow, User>(Json<UserRow>("[ { 'id': 1, 'name': 'test user', 'roles': [ { 'id': 1 }, { 'id': 2 } ] } ]"));
        await store.Mapper.Import<RoleRow, Role>(Json<RoleRow>("[ { 'id': 1, 'name': 'admin' }, { 'id': 2, 'name': 'power user' } ]"));
        await store.Db.SaveChangesAsync();

        await AssertUserWithRoles(store);
    }

    [Fact]
    public async Task import_aggregation_and_compositions()
    {
        await using var store = await MapperStore<ImportContext>.Create();

        // Aggregations are independent rows, expected to be there when the root is mapped.
        store.Db.Add(new InvoiceType { Id = 1, Name = "A" });
        store.Db.Add(new InvoiceType { Id = 2, Name = "B" });
        await store.Db.SaveChangesAsync();

        await store.Mapper.Import<InvoiceRowsDto, Invoice>(Json<InvoiceRowsDto>("""
            [{
                'id': 1,
                'invoiceType': { 'id': 2 },
                'rows': [
                    { 'id': 1, 'description': 'prod 1', 'quantity': 5, 'price': 25 },
                    { 'id': 2, 'description': 'prod 2', 'quantity': 3, 'price': 100 }
                ]
            }]
            """));
        await store.Db.SaveChangesAsync();

        await using (var read = store.Fresh())
        {
            var created = await read.Invoices.Include(i => i.InvoiceType).Include(i => i.Rows).SingleAsync(i => i.Id == 1);

            Assert.Equal("B", created.InvoiceType!.Name);
            Assert.Equal(2, created.Rows!.Count);
            Assert.Contains(created.Rows, r => r.Description == "prod 1" && r.Quantity == 5);
            Assert.Contains(created.Rows, r => r.Description == "prod 2" && r.Quantity == 3);
        }

        await using var update = store.Fresh();

        await MapperStore<ImportContext>.MapperOver(update).Import<InvoiceTypeDto, Invoice>(Json<InvoiceTypeDto>("[{ 'id': 1, 'invoiceType': { 'id': 1 } }]"));
        await update.SaveChangesAsync();

        await using var final = store.Fresh();

        var updated = await final.Invoices.Include(i => i.InvoiceType).Include(i => i.Rows).SingleAsync(i => i.Id == 1);

        Assert.Equal("A", updated.InvoiceType!.Name);
        Assert.Equal(2, updated.Rows!.Count);
        Assert.Contains(updated.Rows, r => r.Description == "prod 1" && r.Quantity == 5);
        Assert.Contains(updated.Rows, r => r.Description == "prod 2" && r.Quantity == 3);
    }

    static List<T> Json<T>(string json)
    {
        return JsonSerializer.Deserialize<List<T>>(json.Replace('\'', '"'), Web)!;
    }

    static async Task AssertUserWithRoles(MapperStore<ImportContext> store)
    {
        await using var read = store.Fresh();

        var user = await read.Users.Include(u => u.Roles).SingleAsync(u => u.Id == 1);

        Assert.Equal("test user", user.Name);
        Assert.Contains(user.Roles!, r => r.Id == 1 && r.Name == "admin");
        Assert.Contains(user.Roles!, r => r.Id == 2 && r.Name == "power user");
    }

    public class UserName
    {
        public int Id { get; set; }

        public string? Name { get; set; }
    }

    public class UserRow
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public List<RoleKey>? Roles { get; set; }
    }

    public class RoleKey
    {
        public int Id { get; set; }
    }

    public class RoleRow
    {
        public int Id { get; set; }

        public string? Name { get; set; }
    }

    public class InvoiceRowsDto
    {
        public int Id { get; set; }

        public InvoiceTypeKey? InvoiceType { get; set; }

        public List<InvoiceRowDto>? Rows { get; set; }
    }

    public class InvoiceTypeDto
    {
        public int Id { get; set; }

        public InvoiceTypeKey? InvoiceType { get; set; }
    }

    public class InvoiceTypeKey
    {
        public int Id { get; set; }
    }

    public class InvoiceRowDto
    {
        public int Id { get; set; }

        public string? Description { get; set; }

        public double Quantity { get; set; }

        public double Price { get; set; }
    }

    [MapFrom(typeof(UserName))]
    [MapFrom(typeof(UserRow))]
    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        public string? Name { get; set; }

        public DateTime DateOfBirth { get; set; }

        public List<Role>? Roles { get; set; }

        [Composition]
        public List<Address>? Addresses { get; set; }

        public UserType? UserType { get; set; }

        [Composition]
        public UserProfile? Profile { get; set; }
    }

    public class UserProfile
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        public string? FirstName { get; set; }

        public string? LastName { get; set; }
    }

    [MapFrom(typeof(RoleRow))]
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

    public class UserType
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        public string? Name { get; set; }

        public List<User>? Users { get; set; }
    }

    public class Address
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        public string? Street { get; set; }

        public string? Number { get; set; }
    }

    [MapFrom(typeof(InvoiceRowsDto))]
    [MapFrom(typeof(InvoiceTypeDto))]
    public class Invoice
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        [Aggregation]
        public InvoiceType? InvoiceType { get; set; }

        [Composition]
        public List<InvoiceRow>? Rows { get; set; }

        [Composition]
        public ShippingAddress? ShippingAddress { get; set; }
    }

    public class InvoiceRow
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        public string? Description { get; set; }

        public double Quantity { get; set; }

        public double Price { get; set; }

        [Composition]
        public List<InvoiceRowDetail>? RowDetails { get; set; }

        public byte[]? RowVersion { get; set; }

        [Parent]
        public Invoice? Invoice { get; set; }
    }

    public class InvoiceRowDetail
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        public string? Description { get; set; }
    }

    public class InvoiceType
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        public string? Name { get; set; }
    }

    [Owned]
    public class ShippingAddress
    {
        public string? Line1 { get; set; }

        public string? Line2 { get; set; }

        public string? Zip { get; set; }
    }

    public class ImportContext(DbContextOptions<ImportContext> options) : DbContext(options)
    {
        public DbSet<User> Users { get; set; } = null!;

        public DbSet<Invoice> Invoices { get; set; } = null!;

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
