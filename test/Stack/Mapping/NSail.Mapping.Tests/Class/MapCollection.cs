// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Collections.ObjectModel;
using NSail.Mapping.Tests.Fixtures;

namespace NSail.Mapping.Tests.Class;

// Ported from Detached.Mappers.Tests/Class/Collection/MapCollection.cs and
// Class/Abstract/MapAbstractConcreteOption.cs. Detached maps a bare list at the root; here the
// list is a member of a declared type.
public sealed class MapCollection
{
    [Fact]
    public void map_collection_of_primitives()
    {
        var result = Mappings.Map<Numbers, Texts>(new Numbers { Values = [1, 2, 3, 4, 5] });

        Assert.Equal(["1", "2", "3", "4", "5"], result.Values!);
    }

    [Fact]
    public void map_complex_collection()
    {
        var source = new SourceType { ComplexCollection = [new SourceItemType { Name = "Item 1" }, new SourceItemType { Name = "Item 2" }, new SourceItemType { Name = "Item 3" }] };

        var target = Mappings.Map<SourceType, TargetType>(source);

        Assert.Equal("Item 1", target.ComplexCollection![0].Name);
        Assert.Equal("Item 2", target.ComplexCollection[1].Name);
        Assert.Equal("Item 3", target.ComplexCollection[2].Name);
    }

    [Fact]
    public void map_list_of_primitives()
    {
        var result = Mappings.Map<Numbers, TextCollection>(new Numbers { Values = [1, 2, 3, 4, 5] });

        Assert.Equal(["1", "2", "3", "4", "5"], result.Values!);
    }

    [Fact]
    public void map_concrete_list()
    {
        var source = new SourceType { ComplexCollection = [new SourceItemType { Name = "Item 1" }, new SourceItemType { Name = "Item 2" }, new SourceItemType { Name = "Item 3" }] };

        var target = Mappings.Map<SourceType, InterfaceTargetType>(source);

        Assert.Equal("Item 1", target.ComplexCollection![0].Name);
        Assert.Equal("Item 2", target.ComplexCollection[1].Name);
        Assert.Equal("Item 3", target.ComplexCollection[2].Name);
    }

    public sealed class Numbers
    {
        public List<int>? Values { get; set; }
    }

    [MapFrom(typeof(Numbers))]
    public sealed class Texts
    {
        public List<string>? Values { get; set; }
    }

    [MapFrom(typeof(Numbers))]
    public sealed class TextCollection
    {
        public Collection<string>? Values { get; set; }
    }

    [MapFrom(typeof(SourceType))]
    public sealed class TargetType
    {
        public List<TargetItemType>? ComplexCollection { get; set; }
    }

    [MapFrom(typeof(SourceType))]
    public sealed class InterfaceTargetType
    {
        public IList<TargetItemType>? ComplexCollection { get; set; }
    }

    public sealed class TargetItemType
    {
        public string? Name { get; set; }
    }

    public sealed class SourceType
    {
        public List<SourceItemType>? ComplexCollection { get; set; }
    }

    public sealed class SourceItemType
    {
        public string? Name { get; set; }
    }
}
