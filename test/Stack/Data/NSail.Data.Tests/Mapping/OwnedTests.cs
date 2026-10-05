// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using NSail.Mapping.Annotations;

namespace NSail.Data.Tests.Mapping;

// Ported from Detached.Mappers.EntityFramework.Tests/Owned/OwnedEntityTests.cs.
public sealed class OwnedTests
{
    [Fact]
    public async Task map_owned_entity()
    {
        await using var store = await MapperStore<OwnedContext>.Create();

        var invoice = await store.Mapper.Upsert<InvoiceDto, Invoice>(new InvoiceDto
        {
            Id = 1,
            ShippingAddress = new ShippingAddressDto { Line1 = "Zeballos St. 2135", Line2 = "Suite 01", Zip = "2000" },
        });

        await store.Db.SaveChangesAsync();

        await using var read = store.Fresh();

        var persisted = await read.Invoices.FirstOrDefaultAsync(i => i.Id == invoice.Id);

        Assert.NotNull(persisted);
        Assert.NotNull(persisted.ShippingAddress);
        Assert.Equal("Zeballos St. 2135", persisted.ShippingAddress.Line1);
        Assert.Equal("Suite 01", persisted.ShippingAddress.Line2);
        Assert.Equal("2000", persisted.ShippingAddress.Zip);
    }

    [Fact]
    public async Task map_owned_entity_update()
    {
        await using var store = await MapperStore<OwnedContext>.Create();

        store.Db.Invoices.Add(new Invoice { Id = 1, ShippingAddress = new ShippingAddress { Line1 = "old", Zip = "1000" } });
        await store.Db.SaveChangesAsync();
        store.Db.ChangeTracker.Clear();

        await store.Mapper.Map<InvoiceDto, Invoice>(new InvoiceDto
        {
            Id = 1,
            ShippingAddress = new ShippingAddressDto { Line1 = "new", Zip = "2000" },
        });

        await store.Db.SaveChangesAsync();

        await using var read = store.Fresh();

        var persisted = await read.Invoices.SingleAsync();

        Assert.Equal("new", persisted.ShippingAddress!.Line1);
        Assert.Equal("2000", persisted.ShippingAddress.Zip);
    }

    [MapFrom(typeof(InvoiceDto))]
    public class Invoice
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        [Composition]
        public ShippingAddress? ShippingAddress { get; set; }
    }

    public class InvoiceDto
    {
        public int Id { get; set; }

        public ShippingAddressDto? ShippingAddress { get; set; }
    }

    [Owned]
    public class ShippingAddress
    {
        public string? Line1 { get; set; }

        public string? Line2 { get; set; }

        public string? Zip { get; set; }
    }

    public class ShippingAddressDto
    {
        public string? Line1 { get; set; }

        public string? Line2 { get; set; }

        public string? Zip { get; set; }
    }

    public class OwnedContext(DbContextOptions<OwnedContext> options) : DbContext(options)
    {
        public DbSet<Invoice> Invoices { get; set; } = null!;
    }
}
