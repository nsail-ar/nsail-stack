// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Mapping.Tests.Fixtures;

namespace NSail.Mapping.Tests.Projection;

// Ported from Detached.Mappers.Tests/Binding/BindInherited.cs. A derived type the row cannot
// carry, and a row told apart by another member, are NSG009 at compile time
// (MapperDiagnosticsTests), so only the valid case runs here.
public sealed class BindInherited
{
    [Fact]
    public void bind_inherited()
    {
        var dto = Mappings.Projected<BaseEntity, BaseDto>()(new ConcreteEntity1 { Id = 1, Name = "Concrete 1", Type = 1, ExtraProp1 = "Extra 1" });

        var concrete = Assert.IsType<ConcreteDto1>(dto);
        Assert.Equal("Concrete 1", concrete.Name);
        Assert.Equal("Extra 1", concrete.ExtraProp1);
    }

    [Fact]
    public void bind_inherited_base()
    {
        var dto = Mappings.Projected<BaseEntity, BaseDto>()(new BaseEntity { Id = 3, Name = "Base" });

        Assert.IsType<BaseDto>(dto);
        Assert.Equal("Base", dto.Name);
    }

    [DiscriminatorName(nameof(Type))]
    [DiscriminatorValue(1, typeof(ConcreteDto1))]
    [DiscriminatorValue(2, typeof(ConcreteDto2))]
    public class BaseDto
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public int Type { get; set; }
    }

    public class ConcreteDto1 : BaseDto
    {
        public string? ExtraProp1 { get; set; }
    }

    public class ConcreteDto2 : BaseDto
    {
        public string? ExtraProp2 { get; set; }
    }

    [MapTo(typeof(BaseDto))]
    [DiscriminatorName(nameof(Type))]
    [DiscriminatorValue(1, typeof(ConcreteEntity1))]
    [DiscriminatorValue(2, typeof(ConcreteEntity2))]
    public class BaseEntity
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public int Type { get; set; }
    }

    public class ConcreteEntity1 : BaseEntity
    {
        public string? ExtraProp1 { get; set; }
    }

    public class ConcreteEntity2 : BaseEntity
    {
        public string? ExtraProp2 { get; set; }
    }
}
