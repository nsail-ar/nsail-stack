// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Mapping.Tests.Fixtures;

namespace NSail.Mapping.Tests.Class;

// Ported from Detached.Mappers.Tests/Class/Primitive/AsPrimitiveSucceedTests.cs. Its failing
// twin (different types under [Primitive]) is a build error here, NSG005, held by the
// generator's own tests.
public sealed class AsPrimitive
{
    [Fact]
    public void map_as_primitive_succeeds()
    {
        var dto = new RootDto
        {
            Mapped = new InnerClass { Name = "mapped class" },
            Copied = new InnerClass { Name = "copied class" },
        };

        var result = Mappings.Map<RootDto, RootEntity>(dto);

        Assert.Equal("copied class", result.Copied!.Name);
        Assert.Equal("mapped class", result.Mapped!.Name);
        Assert.NotSame(dto.Mapped, result.Mapped);
        Assert.Same(dto.Copied, result.Copied);
    }

    public sealed class RootDto
    {
        public InnerClass? Mapped { get; set; }

        public InnerClass? Copied { get; set; }
    }

    [MapFrom(typeof(RootDto))]
    public sealed class RootEntity
    {
        [Composition]
        public InnerClass? Mapped { get; set; }

        [Composition]
        [Primitive]
        public InnerClass? Copied { get; set; }
    }

    public sealed class InnerClass
    {
        public string? Name { get; set; }
    }
}
