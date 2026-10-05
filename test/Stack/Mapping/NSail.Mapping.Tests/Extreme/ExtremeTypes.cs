// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Collections.ObjectModel;
using NSail.Mapping.Tests.Fixtures;

namespace NSail.Mapping.Tests.Extreme;

// Shapes a real model reaches sooner or later and a convention-built mapper tends to trip on:
// members named by keywords, init-only rows, every collection interface, scalars that need a
// conversion on the way in and out, and a graph deep enough to nest every lambda.
public sealed class ExtremeTypes
{
    [Fact]
    public void keyword_named_members_map_and_project()
    {
        var target = Mappings.Map<KeywordDto, Keyword>(new KeywordDto { @class = "a", @event = 2, @namespace = "n" });

        Assert.Equal("a", target.@class);
        Assert.Equal(2, target.@event);
        Assert.Equal("n", target.@namespace);

        var row = Mappings.Projected<Keyword, KeywordDto>()(target);

        Assert.Equal("a", row.@class);
        Assert.Equal(2, row.@event);
    }

    [Fact]
    public void init_only_row_is_projected()
    {
        var row = Mappings.Projected<Keyword, KeywordRow>()(new Keyword { @class = "x", @event = 9 });

        Assert.Equal("x", row.Class);
        Assert.Equal(9, row.Event);
    }

    [Fact]
    public void every_collection_shape_is_filled()
    {
        var items = new List<ItemDto> { new() { Name = "a" }, new() { Name = "b" } };

        var target = Mappings.Map<ShapesDto, Shapes>(new ShapesDto
        {
            AsList = items,
            AsIList = items,
            AsICollection = items,
            AsIEnumerable = items,
            AsIReadOnlyList = items,
            AsArray = items,
            AsHashSet = items,
            AsCollection = items,
            Numbers = [1, 2, 3],
            Words = ["x", "y"],
        });

        Assert.Equal(["a", "b"], target.AsList!.Select(i => i.Name));
        Assert.Equal(["a", "b"], target.AsIList!.Select(i => i.Name));
        Assert.Equal(["a", "b"], target.AsICollection!.Select(i => i.Name));
        Assert.Equal(["a", "b"], target.AsIEnumerable!.Select(i => i.Name));
        Assert.Equal(["a", "b"], target.AsIReadOnlyList!.Select(i => i.Name));
        Assert.Equal(["a", "b"], target.AsArray!.Select(i => i.Name));
        Assert.Equal(2, target.AsHashSet!.Count);
        Assert.Equal(["a", "b"], target.AsCollection!.Select(i => i.Name));
        Assert.Equal([1L, 2L, 3L], target.Numbers!);
        Assert.Equal(["x", "y"], target.Words!);
    }

    [Fact]
    public void scalars_convert_both_ways()
    {
        var target = Mappings.Map<ScalarsDto, Scalars>(new ScalarsDto
        {
            Flags = "Read, Write",
            Status = "closed",
            Day = "2026-10-03",
            Span = "01:30:00",
            Amount = 12.5,
            Count = 7L,
            Nullable = null,
            Blob = [1, 2, 3],
            Marker = Guid.Parse("8f2b4b0e-5f68-4a43-9a3a-36e0c2c4f1a1").ToString(),
        });

        Assert.Equal(Access.Read | Access.Write, target.Flags);
        Assert.Equal(Status.Closed, target.Status);
        Assert.Equal(new DateOnly(2026, 10, 3), target.Day);
        Assert.Equal(TimeSpan.FromMinutes(90), target.Span);
        Assert.Equal(12.5m, target.Amount);
        Assert.Equal(7, target.Count);
        Assert.Null(target.Nullable);
        Assert.Equal([1, 2, 3], target.Blob!);
        Assert.Equal(Guid.Parse("8f2b4b0e-5f68-4a43-9a3a-36e0c2c4f1a1"), target.Marker);

        var row = Mappings.Projected<Scalars, ScalarsRow>()(target);

        Assert.Equal("Closed", row.Status);
        Assert.Equal(12.5, row.Amount);
        Assert.Equal(7L, row.Count);
        Assert.Equal(0, row.Nullable);
        Assert.Equal([1, 2, 3], row.Blob!);
    }

    [Fact]
    public void empty_and_blank_strings_are_no_value()
    {
        var target = Mappings.Map<ScalarsDto, Scalars>(new ScalarsDto { Status = "", Day = null, Nullable = "" });

        Assert.Equal(default, target.Status);
        Assert.Equal(default, target.Day);
        Assert.Null(target.Nullable);
    }

    [Fact]
    public void an_unparsable_string_throws_rather_than_guessing()
    {
        Assert.ThrowsAny<FormatException>(() => Mappings.Map<ScalarsDto, Scalars>(new ScalarsDto { Day = "not a date" }));
    }

    [Fact]
    public void a_deep_graph_maps_and_projects_every_level()
    {
        var source = new Level1Dto { Name = "1", Next = new Level2Dto { Name = "2", Items = [new Level3Dto { Name = "3", Next = new Level4Dto { Name = "4", Tags = ["t"] } }] } };

        var target = Mappings.Map<Level1Dto, Level1>(source);

        Assert.Equal("4", target.Next!.Items![0].Next!.Name);
        Assert.Equal(["t"], target.Next.Items[0].Next!.Tags!);

        var row = Mappings.Projected<Level1, Level1Dto>()(target);

        Assert.Equal("4", row.Next!.Items![0].Next!.Name);
        Assert.Equal(["t"], row.Next.Items[0].Next!.Tags!);
    }

    [Fact]
    public void a_record_message_is_a_source()
    {
        var target = Mappings.Map<RenameRecord, Keyword>(new RenameRecord("r", 5));

        Assert.Equal("r", target.@class);
        Assert.Equal(5, target.@event);
    }

    [Fact]
    public void a_dictionary_is_copied_into_one_of_its_own()
    {
        var source = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };

        var target = Mappings.Map<CountsDto, Counts>(new CountsDto { Values = source });

        Assert.NotSame(source, target.Values);
        Assert.Equal(source, target.Values!);
    }

    public class CountsDto
    {
        public Dictionary<string, int>? Values { get; set; }
    }

    [MapFrom(typeof(CountsDto))]
    public class Counts
    {
        public Dictionary<string, int>? Values { get; set; }
    }

    public class KeywordDto
    {
        public string? @class { get; set; }

        public int @event { get; set; }

        public string? @namespace { get; set; }
    }

    public sealed record RenameRecord(string @class, int @event);

    public sealed class KeywordRow
    {
        public string? Class { get; init; }

        public int Event { get; init; }
    }

    [MapFrom(typeof(KeywordDto))]
    [MapFrom(typeof(RenameRecord))]
    [MapTo(typeof(KeywordDto))]
    [MapTo(typeof(KeywordRow))]
    public class Keyword
    {
        public string? @class { get; set; }

        public int @event { get; set; }

        public string? @namespace { get; set; }
    }

    public class ItemDto
    {
        public string? Name { get; set; }
    }

    public class Item
    {
        public string? Name { get; set; }
    }

    public class ShapesDto
    {
        public List<ItemDto>? AsList { get; set; }

        public List<ItemDto>? AsIList { get; set; }

        public List<ItemDto>? AsICollection { get; set; }

        public List<ItemDto>? AsIEnumerable { get; set; }

        public List<ItemDto>? AsIReadOnlyList { get; set; }

        public List<ItemDto>? AsArray { get; set; }

        public List<ItemDto>? AsHashSet { get; set; }

        public List<ItemDto>? AsCollection { get; set; }

        public int[]? Numbers { get; set; }

        public IEnumerable<string>? Words { get; set; }
    }

    [MapFrom(typeof(ShapesDto))]
    public class Shapes
    {
        public List<Item>? AsList { get; set; }

        public IList<Item>? AsIList { get; set; }

        public ICollection<Item>? AsICollection { get; set; }

        public IEnumerable<Item>? AsIEnumerable { get; set; }

        public IReadOnlyList<Item>? AsIReadOnlyList { get; set; }

        public Item[]? AsArray { get; set; }

        public HashSet<Item>? AsHashSet { get; set; }

        public Collection<Item>? AsCollection { get; set; }

        public List<long>? Numbers { get; set; }

        public string[]? Words { get; set; }
    }

    [Flags]
    public enum Access
    {
        None = 0,
        Read = 1,
        Write = 2,
    }

    public enum Status
    {
        Open,
        Closed,
    }

    public class ScalarsDto
    {
        public string? Flags { get; set; }

        public string? Status { get; set; }

        public string? Day { get; set; }

        public string? Span { get; set; }

        public double Amount { get; set; }

        public long Count { get; set; }

        public string? Nullable { get; set; }

        public byte[]? Blob { get; set; }

        public string? Marker { get; set; }
    }

    [MapFrom(typeof(ScalarsDto))]
    [MapTo(typeof(ScalarsRow))]
    public class Scalars
    {
        public Access Flags { get; set; }

        public Status Status { get; set; }

        public DateOnly Day { get; set; }

        public TimeSpan Span { get; set; }

        public decimal Amount { get; set; }

        public int Count { get; set; }

        public int? Nullable { get; set; }

        public byte[]? Blob { get; set; }

        public Guid Marker { get; set; }
    }

    public class ScalarsRow
    {
        public string? Status { get; set; }

        public double Amount { get; set; }

        public long Count { get; set; }

        public int Nullable { get; set; }

        public byte[]? Blob { get; set; }
    }

    public class Level1Dto
    {
        public string? Name { get; set; }

        public Level2Dto? Next { get; set; }
    }

    public class Level2Dto
    {
        public string? Name { get; set; }

        public List<Level3Dto>? Items { get; set; }
    }

    public class Level3Dto
    {
        public string? Name { get; set; }

        public Level4Dto? Next { get; set; }
    }

    public class Level4Dto
    {
        public string? Name { get; set; }

        public List<string>? Tags { get; set; }
    }

    [MapFrom(typeof(Level1Dto))]
    [MapTo(typeof(Level1Dto))]
    public class Level1
    {
        public string? Name { get; set; }

        public Level2? Next { get; set; }
    }

    public class Level2
    {
        public string? Name { get; set; }

        public List<Level3>? Items { get; set; }
    }

    public class Level3
    {
        public string? Name { get; set; }

        public Level4? Next { get; set; }
    }

    public class Level4
    {
        public string? Name { get; set; }

        public string[]? Tags { get; set; }
    }
}
