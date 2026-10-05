// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Mapping.Tests.Fixtures;

namespace NSail.Mapping.Tests.Class;

// Ported from Detached.Mappers.Tests/Class/Complex/MapComplexWithCycles.cs and
// MapComplexWithCyclesSameType.cs.
public sealed class MapCycles
{
    [Fact]
    public void map_complex_direct_cycle()
    {
        var dto = new DirectCycleDto { Id = 1, Name = "cycledto" };
        dto.Parent = dto;

        var result = Mappings.Map<DirectCycleDto, DirectCycle>(dto);

        Assert.Equal(1, result.Id);
        Assert.Equal("cycledto", result.Name);
        Assert.Same(result, result.Parent);
    }

    [Fact]
    public void map_complex_indirect_cycle()
    {
        var root = new RootDto { Id = 1, Name = "root", Items = [] };
        root.Items.Add(new ItemDto { Id = 1, Name = "item 1", Parent = root });
        root.Items.Add(new ItemDto { Id = 2, Name = "item 2", Parent = root });

        var result = Mappings.Map<RootDto, Root>(root);

        Assert.Equal(1, result.Id);
        Assert.Equal("root", result.Name);
        Assert.Equal(1, result.Items![0].Id);
        Assert.Equal("item 1", result.Items[0].Name);
        Assert.Same(result, result.Items[0].Parent);
        Assert.Equal(2, result.Items[1].Id);
        Assert.Equal("item 2", result.Items[1].Name);
        Assert.Same(result, result.Items[1].Parent);
    }

    [Fact]
    public void map_complex_direct_cycle_sametype()
    {
        var source = new CycleSource
        {
            Text = "sample text",
            DirectCycle = new CycleSource
            {
                Text = "sample cycle",
                DirectCycle = new CycleSource
                {
                    Text = "sample cycle 2",
                    DirectCycle = new CycleSource
                    {
                        Text = "sample cycle 3",
                        DirectCycle = new CycleSource { Text = "sample cycle 4" },
                    },
                },
            },
        };

        var mapped = Mappings.Map<CycleSource, CycleTarget>(source);

        Assert.Equal("sample text", mapped.Text);
        Assert.Equal("sample cycle", mapped.DirectCycle!.Text);
        Assert.Equal("sample cycle 2", mapped.DirectCycle.DirectCycle!.Text);
        Assert.Equal("sample cycle 3", mapped.DirectCycle.DirectCycle.DirectCycle!.Text);
        Assert.Equal("sample cycle 4", mapped.DirectCycle.DirectCycle.DirectCycle.DirectCycle!.Text);
    }

    [Fact]
    public void map_complex_indirect_cycle_sametype()
    {
        var source = new CycleA
        {
            Name = "A1",
            B = new CycleB { Name = "B1", C = new CycleC { Name = "C1", A = new CycleA { Name = "A2" } } },
        };

        var result = Mappings.Map(source, new CycleA());

        Assert.Equal("A1", result.Name);
        Assert.Equal("B1", result.B!.Name);
        Assert.Equal("C1", result.B.C!.Name);
        Assert.Equal("A2", result.B.C.A!.Name);
    }

    public sealed class DirectCycleDto
    {
        public DirectCycleDto? Parent { get; set; }

        public int Id { get; set; }

        public string? Name { get; set; }
    }

    [MapFrom(typeof(DirectCycleDto))]
    public sealed class DirectCycle
    {
        public DirectCycle? Parent { get; set; }

        public int Id { get; set; }

        public string? Name { get; set; }
    }

    public sealed class RootDto
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public List<ItemDto>? Items { get; set; }
    }

    [MapFrom(typeof(RootDto))]
    public sealed class Root
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public List<Item>? Items { get; set; }
    }

    public sealed class ItemDto
    {
        public RootDto? Parent { get; set; }

        public int Id { get; set; }

        public string? Name { get; set; }
    }

    public sealed class Item
    {
        public Root? Parent { get; set; }

        public int Id { get; set; }

        public string? Name { get; set; }
    }

    [MapFrom(typeof(CycleSource))]
    public sealed class CycleTarget
    {
        public string? Text { get; set; }

        public CycleTarget? DirectCycle { get; set; }
    }

    public sealed class CycleSource
    {
        public string? Text { get; set; }

        public CycleSource? DirectCycle { get; set; }
    }

    [MapFrom(typeof(CycleA))]
    public sealed class CycleA
    {
        public string? Name { get; set; }

        public CycleB? B { get; set; }
    }

    public sealed class CycleB
    {
        public string? Name { get; set; }

        public CycleC? C { get; set; }
    }

    public sealed class CycleC
    {
        public string? Name { get; set; }

        public CycleA? A { get; set; }
    }
}
