// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using NSail.Mapping;
using NSail.Mapping.Annotations;

namespace NSail.Data.Tests.Mapping;

// Ported from Detached.Mappers.EntityFramework.Tests/Inheritance/InheritedEntityTests.cs. The
// source is one type carrying every derived type's members instead of anonymous objects; a base
// with no discriminator is NSG007 at compile time, so that case has no runtime test.
public sealed class InheritanceTests
{
    [Fact]
    public async Task map_inherited_entity()
    {
        await using var store = await MapperStore<InheritanceContext>.Create();

        var sellPoint = await store.Mapper.Upsert<SellPointDto, SellPoint>(new SellPointDto
        {
            Id = 1,
            DeliveryAreas =
            [
                new DeliveryAreaDto { Id = 1, AreaType = DeliveryAreaType.Circle, X = 0.25, Y = 0.25, Radius = 10.0 },
                new DeliveryAreaDto { Id = 2, AreaType = DeliveryAreaType.Rectangle, X1 = 0.25, Y1 = 0.25, X2 = 1, Y2 = 1 },
            ],
        });

        Assert.Equal(2, sellPoint.DeliveryAreas!.Count);
        Assert.IsType<CircleDeliveryArea>(sellPoint.DeliveryAreas[0]);
        Assert.IsType<RectangleDeliveryArea>(sellPoint.DeliveryAreas[1]);

        await store.Db.SaveChangesAsync();

        await using var read = store.Fresh();

        var persisted = await read.SellPoints.Include(s => s.DeliveryAreas).SingleAsync();

        var circle = Assert.IsType<CircleDeliveryArea>(persisted.DeliveryAreas!.Single(d => d.Id == 1));
        Assert.Equal(10.0, circle.Radius);
        var rectangle = Assert.IsType<RectangleDeliveryArea>(persisted.DeliveryAreas!.Single(d => d.Id == 2));
        Assert.Equal(1, rectangle.X2);
    }

    [Fact]
    public async Task map_inherited_entity_discriminator()
    {
        await using var store = await MapperStore<InheritanceContext>.Create();

        await Assert.ThrowsAsync<MapperException>(() => store.Mapper.Upsert<SellPointDto, SellPoint>(new SellPointDto
        {
            Id = 1,
            DeliveryAreas = [new DeliveryAreaDto { Id = 1, AreaType = (DeliveryAreaType)50, X = 0.25, Y = 0.25, Radius = 10.0 }],
        }));
    }

    public class SellPointDto
    {
        public int Id { get; set; }

        public List<DeliveryAreaDto>? DeliveryAreas { get; set; }
    }

    public class DeliveryAreaDto
    {
        public int Id { get; set; }

        public DeliveryAreaType AreaType { get; set; }

        public double X { get; set; }

        public double Y { get; set; }

        public double Radius { get; set; }

        public double X1 { get; set; }

        public double Y1 { get; set; }

        public double X2 { get; set; }

        public double Y2 { get; set; }
    }

    [MapFrom(typeof(SellPointDto))]
    public class SellPoint
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        [Composition]
        public List<DeliveryArea>? DeliveryAreas { get; set; }
    }

    public enum DeliveryAreaType
    {
        Rectangle,
        Circle,
    }

    [DiscriminatorName(nameof(AreaType))]
    [DiscriminatorValue(DeliveryAreaType.Circle, typeof(CircleDeliveryArea))]
    [DiscriminatorValue(DeliveryAreaType.Rectangle, typeof(RectangleDeliveryArea))]
    public abstract class DeliveryArea
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        public DeliveryAreaType AreaType { get; set; }
    }

    public class RectangleDeliveryArea : DeliveryArea
    {
        public double X1 { get; set; }

        public double Y1 { get; set; }

        public double X2 { get; set; }

        public double Y2 { get; set; }
    }

    public class CircleDeliveryArea : DeliveryArea
    {
        public double X { get; set; }

        public double Y { get; set; }

        public double Radius { get; set; }
    }

    public class InheritanceContext(DbContextOptions<InheritanceContext> options) : DbContext(options)
    {
        public DbSet<SellPoint> SellPoints { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DeliveryArea>().HasDiscriminator(d => d.AreaType)
                .HasValue<CircleDeliveryArea>(DeliveryAreaType.Circle)
                .HasValue<RectangleDeliveryArea>(DeliveryAreaType.Rectangle);
        }
    }
}
