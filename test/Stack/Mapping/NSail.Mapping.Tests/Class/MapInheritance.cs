// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Mapping.Tests.Fixtures;

namespace NSail.Mapping.Tests.Class;

// Ported from Detached.Mappers.Tests/Class/Inherited/MapInheritanceAnnotated.cs. Detached maps
// a list of anonymous objects; here the source is one type carrying every derived type's
// members, which is the shape a message has.
public sealed class MapInheritance
{
    [Fact]
    public void map_inherited_using_discriminator()
    {
        var zoo = new ZooDto
        {
            Animals =
            [
                new AnimalDto { AnimalType = "cat", Id = 1, Name = "Snarf", HairType = HairType.Short },
                new AnimalDto { AnimalType = "fish", Id = 2, Name = "Nemo", HasTeeth = true },
            ],
        };

        var result = Mappings.Map<ZooDto, Zoo>(zoo).Animals!;

        var cat = Assert.IsType<Cat>(result[0]);
        Assert.Equal(1, cat.Id);
        Assert.Equal("Snarf", cat.Name);
        Assert.Equal(HairType.Short, cat.HairType);

        var fish = Assert.IsType<Fish>(result[1]);
        Assert.Equal(2, fish.Id);
        Assert.Equal("Nemo", fish.Name);
        Assert.True(fish.HasTeeth);
    }

    [Fact]
    public void an_unknown_discriminator_is_refused()
    {
        var zoo = new ZooDto { Animals = [new AnimalDto { AnimalType = "dragon", Id = 3 }] };

        Assert.Throws<MapperException>(() => Mappings.Map<ZooDto, Zoo>(zoo));
    }

    public sealed class ZooDto
    {
        public List<AnimalDto>? Animals { get; set; }
    }

    public sealed class AnimalDto
    {
        public string? AnimalType { get; set; }

        public int Id { get; set; }

        public string? Name { get; set; }

        public HairType HairType { get; set; }

        public bool HasTeeth { get; set; }
    }

    [MapFrom(typeof(ZooDto))]
    public sealed class Zoo
    {
        public List<Animal>? Animals { get; set; }
    }

    [DiscriminatorName(nameof(AnimalType))]
    [DiscriminatorValue("cat", typeof(Cat))]
    [DiscriminatorValue("fish", typeof(Fish))]
    public abstract class Animal
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public string? AnimalType { get; set; }
    }

    public sealed class Fish : Animal
    {
        public bool HasTeeth { get; set; }
    }

    public sealed class Cat : Animal
    {
        public HairType HairType { get; set; }
    }

    public enum HairType
    {
        Long,
        Short,
    }
}
