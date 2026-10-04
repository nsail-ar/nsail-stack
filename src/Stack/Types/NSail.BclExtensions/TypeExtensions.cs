// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Collections;

namespace NSail.BclExtensions;

public static class TypeExtensions
{
    private static readonly HashSet<Type> ScalarTypes = new HashSet<Type>
    {
        typeof(bool),
        typeof(byte),
        typeof(sbyte),
        typeof(short),
        typeof(ushort),
        typeof(int),
        typeof(uint),
        typeof(long),
        typeof(ulong),
        typeof(float),
        typeof(double),
        typeof(decimal),
        typeof(char),
        typeof(string),
        typeof(Guid),
        typeof(DateTime),
        typeof(DateOnly),
        typeof(TimeOnly)
    };

    public static bool IsScalar(this Type type)
    {
        if (type.IsEnum)
        {
            return true;
        }

        if (ScalarTypes.Contains(type))
        {
            return true;
        }

        if (IsNullableScalar(type))
        {
            return true;
        }

        return false;
    }

    public static bool IsCollection(this Type type)
    {
        if (type == typeof(string))
        {
            return false;
        }

        if (type.IsArray)
        {
            return true;
        }

        if (typeof(IEnumerable).IsAssignableFrom(type))
        {
            return true;
        }

        return false;
    }

    public static bool IsNullableScalar(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            Type inner = type.GetGenericArguments()[0];
            return inner.IsScalar();
        }

        return false;
    }
}