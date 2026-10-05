// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Mapping.Tests.Fixtures;

namespace NSail.Mapping.Tests.Class;

// Ported from Detached.Mappers.Tests/Class/Complex/MapBasicComplex.cs, MapNestedComplex.cs and
// MapIgnoreTests.cs. The anonymous-type cases are not ported: a generated mapper is declared on
// a type the compiler can name.
public sealed class MapComplex
{
    [Fact]
    public void map_same_type()
    {
        var targetObj = new TargetTestType { Text = "sample text", Number = 5 };

        var mapped = Mappings.Map<TargetTestType, TargetTestType>(targetObj);

        Assert.NotSame(targetObj, mapped);
        Assert.Equal("sample text", mapped.Text);
        Assert.Equal(5, mapped.Number);
    }

    [Fact]
    public void map_different_types()
    {
        var targetObj = new TargetTestType { Text = "sample text", Number = 5, DateTime = new DateTime(1984, 07, 09) };
        var sourceObj = new SourceTestType { Text = "new text", Number = 9 };

        var mapped = Mappings.Map(sourceObj, targetObj);

        Assert.Same(targetObj, mapped);
        Assert.Equal("new text", mapped.Text);
        Assert.Equal(9, mapped.Number);
        Assert.Equal(new DateTime(1984, 07, 09), mapped.DateTime);
    }

    [Fact]
    public void map_nested_complex_same_type()
    {
        var oldObj = new NestedTargetType { Text = "sample text", Number = 5, Nested = new NestedChildTargetType { Number = 9, Text = "sample nested" } };

        var mapped = Mappings.Map<NestedTargetType, NestedTargetType>(oldObj);

        Assert.NotSame(oldObj, mapped);
        Assert.Equal("sample text", mapped.Text);
        Assert.Equal(5, mapped.Number);
        Assert.Equal("sample nested", mapped.Nested!.Text);
        Assert.Equal(9, mapped.Nested.Number);
    }

    [Fact]
    public void map_nested_complex_different_type()
    {
        var targetObj = new NestedTargetType { Text = "sample text", Number = 5, Nested = new NestedChildTargetType { Number = 9, Text = "sample nested" } };
        var sourceObj = new NestedSourceType { Text = "new text", Number = 9, Nested = new NestedChildSourceType { Number = 1, Text = "other sample nested" } };

        var mapped = Mappings.Map(sourceObj, targetObj);

        Assert.Same(targetObj, mapped);
        Assert.Equal("new text", mapped.Text);
        Assert.Equal(9, mapped.Number);
        Assert.Equal("other sample nested", mapped.Nested!.Text);
        Assert.Equal(1, mapped.Nested.Number);
    }

    [Fact]
    public void map_nested_null_member()
    {
        var targetObj = new NestedTargetType { Text = "sample text", Number = 5, Nested = new NestedChildTargetType { Number = 9, Text = "sample nested" } };
        var sourceObj = new NestedSourceType { Text = "new text", Number = 9, Nested = null };

        var mapped = Mappings.Map(sourceObj, targetObj);

        Assert.Same(targetObj, mapped);
        Assert.Equal("new text", mapped.Text);
        Assert.Equal(9, mapped.Number);
        Assert.Null(mapped.Nested);
    }

    [Fact]
    public void ignore_not_mapped_property()
    {
        var source = new IgnoringEntity { Text1 = "target_text1", Text2 = "target_text2" };
        var target = new IgnoringEntity { Text1 = "source_text1", Text2 = "source_text2" };

        var mapped = Mappings.Map(source, target);

        Assert.Equal("target_text1", mapped.Text1);
        Assert.Equal("source_text2", mapped.Text2);
    }

    [MapFrom(typeof(TargetTestType))]
    [MapFrom(typeof(SourceTestType))]
    public sealed class TargetTestType
    {
        public string? Text { get; set; }

        public int Number { get; set; }

        public DateTime DateTime { get; set; }
    }

    public sealed class SourceTestType
    {
        public string? Text { get; set; }

        public int Number { get; set; }
    }

    [MapFrom(typeof(NestedTargetType))]
    [MapFrom(typeof(NestedSourceType))]
    public sealed class NestedTargetType
    {
        public string? Text { get; set; }

        public int Number { get; set; }

        public NestedChildTargetType? Nested { get; set; }
    }

    public sealed class NestedChildTargetType
    {
        public string? Text { get; set; }

        public int Number { get; set; }
    }

    public sealed class NestedSourceType
    {
        public string? Text { get; set; }

        public int Number { get; set; }

        public NestedChildSourceType? Nested { get; set; }
    }

    public sealed class NestedChildSourceType
    {
        public string? Text { get; set; }

        public int Number { get; set; }
    }

    [MapFrom(typeof(IgnoringEntity))]
    public sealed class IgnoringEntity
    {
        public string? Text1 { get; set; }

        [MapIgnore]
        public string? Text2 { get; set; }
    }
}
