// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Mapping.Tests.Fixtures;

namespace NSail.Mapping.Tests.Projection;

// A query cannot recurse: the member that walks back into a type already being read is left
// out, once, instead of the generator never finishing.
public sealed class BindCycles
{
    [Fact]
    public void bind_cycle_reads_once()
    {
        var parent = new Node { Id = 1 };
        parent.Child = new Node { Id = 2, Parent = parent };

        var dto = Mappings.Projected<Node, NodeDto>()(parent);

        Assert.Equal(1, dto.Id);
        Assert.Null(dto.Child);
    }

    public class NodeDto
    {
        public int Id { get; set; }

        public NodeDto? Child { get; set; }

        public NodeDto? Parent { get; set; }
    }

    [MapTo(typeof(NodeDto))]
    public class Node
    {
        public int Id { get; set; }

        public Node? Child { get; set; }

        public Node? Parent { get; set; }
    }
}
