// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Diagnostics;
using NSail.Mapping.Tests.Fixtures;

namespace NSail.Mapping.Tests.Extreme;

// The graph at its limits: one source object reached twice, the same key twice in one list, a
// chain deeper than any screen sends, a list bigger than any screen shows, and a hierarchy
// three levels down.
public sealed class ExtremeGraphs
{
    [Fact]
    public void a_source_reached_twice_is_one_target()
    {
        var shared = new NodeDto { Name = "shared" };

        var target = Mappings.Map<PairDto, Pair>(new PairDto { Left = shared, Right = shared });

        Assert.Same(target.Left, target.Right);
    }

    [Fact]
    public void the_same_key_twice_in_a_composition_is_one_entity()
    {
        var context = new RecordingContext();

        var target = Mappings.Map<OrderDto, Order>(new OrderDto
        {
            Id = 1,
            Lines = [new LineDto { Id = 7, Text = "first" }, new LineDto { Id = 7, Text = "second" }],
        }, null, context);

        var line = Assert.Single(target.Lines!);
        Assert.Equal(7, line.Id);
        Assert.Equal("first", line.Text);
        Assert.True(context.Verify<Line>(7, out var action));
        Assert.Equal(MapAction.Create, action);
    }

    [Fact]
    public void kept_children_are_the_same_instances_in_the_source_order()
    {
        var a = new Line { Id = 1, Text = "a" };
        var b = new Line { Id = 2, Text = "b" };
        var c = new Line { Id = 3, Text = "c" };
        var order = new Order { Id = 1, Lines = [a, b, c] };
        var lines = order.Lines;

        Mappings.Map(new OrderDto { Id = 1, Lines = [new LineDto { Id = 3, Text = "C" }, new LineDto { Id = 4, Text = "d" }, new LineDto { Id = 1, Text = "A" }] }, order, new RecordingContext());

        Assert.Same(lines, order.Lines);
        Assert.Equal([3, 4, 1], order.Lines!.Select(l => l.Id));
        Assert.Same(c, order.Lines[0]);
        Assert.Same(a, order.Lines[2]);
        Assert.Equal("C", c.Text);
        Assert.Equal("A", a.Text);
    }

    [Fact]
    public void a_null_composition_empties_it_and_an_empty_one_too()
    {
        var context = new RecordingContext();
        var order = new Order { Id = 1, Lines = [new Line { Id = 1 }, new Line { Id = 2 }] };

        Mappings.Map(new OrderDto { Id = 1, Lines = null }, order, context);

        Assert.Empty(order.Lines!);
        Assert.True(context.Verify<Line>(1, out var first));
        Assert.Equal(MapAction.Delete, first);

        order.Lines!.Add(new Line { Id = 3 });

        Mappings.Map(new OrderDto { Id = 1, Lines = [] }, order, context);

        Assert.Empty(order.Lines);
    }

    [Fact]
    public void appending_keeps_what_the_source_does_not_name()
    {
        var order = new Order { Id = 1, Lines = [new Line { Id = 1 }] };
        var context = new RecordingContext(new MapParameters { CompositeCollectionBehavior = CompositeCollectionBehavior.Append });

        Mappings.Map(new OrderDto { Id = 1, Lines = [new LineDto { Id = 2 }] }, order, context);

        Assert.Equal([2, 1], order.Lines!.Select(l => l.Id));
    }

    [Fact]
    public async Task a_chain_a_thousand_deep_maps_without_overflowing()
    {
        var head = new NodeDto { Name = "0" };
        var current = head;

        for (var i = 1; i < 1000; i++)
        {
            current.Next = new NodeDto { Name = i.ToString() };
            current = current.Next;
        }

        // A thread of its own with a small stack: a mapper that recursed synchronously per level
        // would overflow here first.
        Node? result = null;
        var thread = new Thread(() => result = Mappings.Map<NodeDto, Node>(head), 256 * 1024);

        thread.Start();
        await Task.Run(thread.Join);

        var depth = 0;

        for (var node = result; node is not null; node = node.Next)
        {
            depth++;
        }

        Assert.Equal(1000, depth);
    }

    [Fact]
    public void a_hundred_thousand_children_merge_in_linear_time()
    {
        const int count = 100_000;

        var order = new Order { Id = 1, Lines = Enumerable.Range(1, count).Select(i => new Line { Id = i, Text = "old" }).ToList() };

        // Every other row kept and rewritten, the rest deleted, as many new ones added.
        var source = new OrderDto
        {
            Id = 1,
            Lines = Enumerable.Range(1, count).Where(i => i % 2 == 0).Concat(Enumerable.Range(count + 1, count / 2)).Select(i => new LineDto { Id = i, Text = "new" }).ToList(),
        };

        var clock = Stopwatch.StartNew();

        Mappings.Map(source, order, new MapContext());

        clock.Stop();

        Assert.Equal(count, order.Lines!.Count);
        Assert.All(order.Lines, l => Assert.Equal("new", l.Text));
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5), $"Merging {count} children took {clock.Elapsed}.");
    }

    [Fact]
    public void a_third_level_derived_type_is_picked_by_discriminator()
    {
        var target = Mappings.Map<ShapeHolderDto, ShapeHolder>(new ShapeHolderDto
        {
            Shapes =
            [
                new ShapeDto { Kind = "square", Side = 2 },
                new ShapeDto { Kind = "rectangle", Side = 3, Height = 4 },
                new ShapeDto { Kind = "circle", Radius = 1 },
            ],
        });

        var square = Assert.IsType<Square>(target.Shapes![0]);
        Assert.Equal(2, square.Side);
        var rectangle = Assert.IsType<Rectangle>(target.Shapes[1]);
        Assert.Equal(4, rectangle.Height);
        Assert.IsType<Circle>(target.Shapes[2]);
    }

    public class NodeDto
    {
        public string? Name { get; set; }

        public NodeDto? Next { get; set; }
    }

    [MapFrom(typeof(NodeDto))]
    public class Node
    {
        public string? Name { get; set; }

        public Node? Next { get; set; }
    }

    public class PairDto
    {
        public NodeDto? Left { get; set; }

        public NodeDto? Right { get; set; }
    }

    [MapFrom(typeof(PairDto))]
    public class Pair
    {
        public Node? Left { get; set; }

        public Node? Right { get; set; }
    }

    public class OrderDto
    {
        public int Id { get; set; }

        public List<LineDto>? Lines { get; set; }
    }

    public class LineDto
    {
        public int Id { get; set; }

        public string? Text { get; set; }
    }

    [Entity]
    [MapFrom(typeof(OrderDto))]
    public class Order
    {
        public int Id { get; set; }

        [Composition]
        public List<Line>? Lines { get; set; }
    }

    [Entity]
    public class Line
    {
        public int Id { get; set; }

        public string? Text { get; set; }
    }

    public class ShapeHolderDto
    {
        public List<ShapeDto>? Shapes { get; set; }
    }

    public class ShapeDto
    {
        public string? Kind { get; set; }

        public double Side { get; set; }

        public double Height { get; set; }

        public double Radius { get; set; }
    }

    [MapFrom(typeof(ShapeHolderDto))]
    public class ShapeHolder
    {
        public List<Shape>? Shapes { get; set; }
    }

    [DiscriminatorName(nameof(Kind))]
    [DiscriminatorValue("square", typeof(Square))]
    [DiscriminatorValue("rectangle", typeof(Rectangle))]
    [DiscriminatorValue("circle", typeof(Circle))]
    public abstract class Shape
    {
        public string? Kind { get; set; }
    }

    public class Square : Shape
    {
        public double Side { get; set; }
    }

    public class Rectangle : Square
    {
        public double Height { get; set; }
    }

    public class Circle : Shape
    {
        public double Radius { get; set; }
    }
}
