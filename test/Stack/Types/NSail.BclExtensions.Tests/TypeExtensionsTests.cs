// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Collections;
using NSail.BclExtensions;

namespace NSail.BclExtensions.Tests;

public sealed class TypeExtensionsTests
{
    #region IsScalar Tests

    [Theory]
    [InlineData(typeof(bool))]
    [InlineData(typeof(byte))]
    [InlineData(typeof(sbyte))]
    [InlineData(typeof(short))]
    [InlineData(typeof(ushort))]
    [InlineData(typeof(int))]
    [InlineData(typeof(uint))]
    [InlineData(typeof(long))]
    [InlineData(typeof(ulong))]
    [InlineData(typeof(float))]
    [InlineData(typeof(double))]
    [InlineData(typeof(decimal))]
    [InlineData(typeof(char))]
    [InlineData(typeof(string))]
    [InlineData(typeof(Guid))]
    [InlineData(typeof(DateTime))]
    [InlineData(typeof(DateOnly))]
    [InlineData(typeof(TimeOnly))]
    public void IsScalar_WithScalarTypes_ReturnsTrue(Type type)
    {
        var result = type.IsScalar();

        Assert.True(result);
    }

    [Theory]
    [InlineData(typeof(int?))]
    [InlineData(typeof(long?))]
    [InlineData(typeof(bool?))]
    [InlineData(typeof(double?))]
    [InlineData(typeof(decimal?))]
    [InlineData(typeof(Guid?))]
    [InlineData(typeof(DateTime?))]
    [InlineData(typeof(DateOnly?))]
    [InlineData(typeof(TimeOnly?))]
    public void IsScalar_WithNullableScalarTypes_ReturnsTrue(Type type)
    {
        var result = type.IsScalar();

        Assert.True(result);
    }

    [Fact]
    public void IsScalar_WithEnum_ReturnsTrue()
    {
        var result = typeof(DayOfWeek).IsScalar();

        Assert.True(result);
    }

    [Fact]
    public void IsScalar_WithNullableEnum_ReturnsTrue()
    {
        var result = typeof(DayOfWeek?).IsScalar();

        Assert.True(result);
    }

    [Fact]
    public void IsScalar_WithCustomEnum_ReturnsTrue()
    {
        var result = typeof(TestEnum).IsScalar();

        Assert.True(result);
    }

    [Theory]
    [InlineData(typeof(object))]
    [InlineData(typeof(int[]))]
    [InlineData(typeof(List<int>))]
    [InlineData(typeof(Dictionary<string, int>))]
    [InlineData(typeof(IEnumerable<int>))]
    public void IsScalar_WithNonScalarTypes_ReturnsFalse(Type type)
    {
        var result = type.IsScalar();

        Assert.False(result);
    }

    [Fact]
    public void IsScalar_WithCustomClass_ReturnsFalse()
    {
        var result = typeof(TestClass).IsScalar();

        Assert.False(result);
    }

    [Fact]
    public void IsScalar_WithCustomStruct_ReturnsFalse()
    {
        var result = typeof(TestStruct).IsScalar();

        Assert.False(result);
    }

    #endregion

    #region IsCollection Tests

    [Fact]
    public void IsCollection_WithString_ReturnsFalse()
    {
        var result = typeof(string).IsCollection();

        Assert.False(result);
    }

    [Theory]
    [InlineData(typeof(int[]))]
    [InlineData(typeof(string[]))]
    [InlineData(typeof(object[]))]
    [InlineData(typeof(byte[]))]
    public void IsCollection_WithArrayTypes_ReturnsTrue(Type type)
    {
        var result = type.IsCollection();

        Assert.True(result);
    }

    [Theory]
    [InlineData(typeof(List<int>))]
    [InlineData(typeof(List<string>))]
    [InlineData(typeof(HashSet<int>))]
    [InlineData(typeof(Dictionary<string, int>))]
    [InlineData(typeof(Queue<int>))]
    [InlineData(typeof(Stack<int>))]
    public void IsCollection_WithGenericCollections_ReturnsTrue(Type type)
    {
        var result = type.IsCollection();

        Assert.True(result);
    }

    [Theory]
    [InlineData(typeof(IEnumerable<int>))]
    [InlineData(typeof(ICollection<int>))]
    [InlineData(typeof(IList<int>))]
    [InlineData(typeof(IEnumerable))]
    public void IsCollection_WithEnumerableInterfaces_ReturnsTrue(Type type)
    {
        var result = type.IsCollection();

        Assert.True(result);
    }

    [Fact]
    public void IsCollection_WithCustomCollectionClass_ReturnsTrue()
    {
        var result = typeof(CustomCollection).IsCollection();

        Assert.True(result);
    }

    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(bool))]
    [InlineData(typeof(DateTime))]
    [InlineData(typeof(Guid))]
    public void IsCollection_WithScalarTypes_ReturnsFalse(Type type)
    {
        var result = type.IsCollection();

        Assert.False(result);
    }

    [Fact]
    public void IsCollection_WithCustomClass_ReturnsFalse()
    {
        var result = typeof(TestClass).IsCollection();

        Assert.False(result);
    }

    [Fact]
    public void IsCollection_WithObject_ReturnsFalse()
    {
        var result = typeof(object).IsCollection();

        Assert.False(result);
    }

    #endregion

    #region IsNullableScalar Tests

    [Theory]
    [InlineData(typeof(int?))]
    [InlineData(typeof(bool?))]
    [InlineData(typeof(double?))]
    [InlineData(typeof(decimal?))]
    [InlineData(typeof(Guid?))]
    [InlineData(typeof(DateTime?))]
    [InlineData(typeof(DateOnly?))]
    [InlineData(typeof(TimeOnly?))]
    public void IsNullableScalar_WithNullableScalarTypes_ReturnsTrue(Type type)
    {
        var result = TypeExtensions.IsNullableScalar(type);

        Assert.True(result);
    }

    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(bool))]
    [InlineData(typeof(string))]
    [InlineData(typeof(object))]
    public void IsNullableScalar_WithNonNullableTypes_ReturnsFalse(Type type)
    {
        var result = TypeExtensions.IsNullableScalar(type);

        Assert.False(result);
    }

    [Fact]
    public void IsNullableScalar_WithNullableEnum_ReturnsTrue()
    {
        var result = TypeExtensions.IsNullableScalar(typeof(DayOfWeek?));

        Assert.True(result);
    }

    [Fact]
    public void IsNullableScalar_WithNullableStruct_ReturnsFalse()
    {
        var result = TypeExtensions.IsNullableScalar(typeof(TestStruct?));

        Assert.False(result);
    }

    [Fact]
    public void IsNullableScalar_WithNullableCustomStruct_ReturnsFalse()
    {
        var result = TypeExtensions.IsNullableScalar(typeof(Nullable<TestStruct>));

        Assert.False(result);
    }

    #endregion

    #region Combined Scenarios

    [Fact]
    public void StringType_IsScalarButNotCollection()
    {
        var type = typeof(string);

        Assert.True(type.IsScalar());
        Assert.False(type.IsCollection());
    }

    [Fact]
    public void ArrayType_IsCollectionButNotScalar()
    {
        var type = typeof(int[]);

        Assert.True(type.IsCollection());
        Assert.False(type.IsScalar());
    }

    [Fact]
    public void CustomClass_IsNeitherScalarNorCollection()
    {
        var type = typeof(TestClass);

        Assert.False(type.IsScalar());
        Assert.False(type.IsCollection());
    }

    [Fact]
    public void EnumType_IsScalarButNotCollection()
    {
        var type = typeof(DayOfWeek);

        Assert.True(type.IsScalar());
        Assert.False(type.IsCollection());
    }

    #endregion

    #region Test Helper Types

    private enum TestEnum
    {
        Value1,
        Value2,
        Value3
    }

    private class TestClass
    {
        public int Value { get; set; }
    }

    private struct TestStruct
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    private class CustomCollection : IEnumerable<int>
    {
        private readonly List<int> _items = new();

        public IEnumerator<int> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    #endregion
}
