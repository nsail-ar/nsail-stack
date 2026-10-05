// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using NSail.Mapping.Annotations;

namespace NSail.Data.Tests.Mapping;

// Ported from Detached.Mappers.EntityFramework.Tests/ValueConverters/*.cs: a [Primitive] member
// the store converts to one column is replaced whole, never merged.
public sealed class ValueConverterTests
{
    [Fact]
    public async Task map_member_with_value_converter()
    {
        await using var store = await MapperStore<ValueConverterContext>.Create();

        var address = await store.Mapper.Upsert<Address, Address>(new Address
        {
            Street = "Main St.",
            Number = "123",
            Tags = [new Tag { Name = "primary" }, new Tag { Name = "local" }],
        });

        await store.Db.SaveChangesAsync();

        await store.Mapper.Map<Address, Address>(new Address
        {
            Id = address.Id,
            Street = "Main St.",
            Number = "1234",
            Tags = [new Tag { Name = "primary" }, new Tag { Name = "external" }],
        });

        await store.Db.SaveChangesAsync();

        await using var read = store.Fresh();

        var persisted = await read.Addresses.SingleAsync(a => a.Id == address.Id);

        Assert.Equal("Main St.", persisted.Street);
        Assert.Equal("1234", persisted.Number);
        Assert.Equal(["primary", "external"], persisted.Tags!.Select(t => t.Name));
    }

    [MapFrom(typeof(Address))]
    public class Address
    {
        [Key]
        public int Id { get; set; }

        public string? Street { get; set; }

        public string? Number { get; set; }

        [Primitive]
        public List<Tag>? Tags { get; set; }
    }

    public class Tag
    {
        public int Id { get; set; }

        public string? Name { get; set; }
    }

    public class ValueConverterContext(DbContextOptions<ValueConverterContext> options) : DbContext(options)
    {
        public DbSet<Address> Addresses { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Address>().Property(a => a.Tags).HasConversion(
                tags => string.Join(", ", tags!.Select(t => t.Name)),
                text => text.Split(", ", StringSplitOptions.RemoveEmptyEntries).Select(n => new Tag { Name = n }).ToList(),
                new ValueComparer<List<Tag>?>(
                    (a, b) => string.Join(", ", a!.Select(t => t.Name)) == string.Join(", ", b!.Select(t => t.Name)),
                    tags => string.Join(", ", tags!.Select(t => t.Name)).GetHashCode(),
                    tags => tags!.Select(t => new Tag { Name = t.Name }).ToList()));
        }
    }
}
