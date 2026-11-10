// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Reflection;

namespace NSail.Components.Tests;

/// <summary>No public member of the renderer takes, returns or exposes a MudBlazor type: the
/// vendor stays inside <c>NSail.Components.Mud</c>.</summary>
public sealed class PublicSurfaceHasNoVendorTypesTests
{
    const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
        | BindingFlags.Static | BindingFlags.DeclaredOnly;

    [Fact]
    public void NoPublicMemberOfTheMudAssemblyNamesAMudType()
    {
        var offenders = new List<string>();

        foreach (var type in typeof(NsMudLocalizer).Assembly.GetExportedTypes())
        {
            if (IsVendor(type.BaseType))
            {
                offenders.Add($"{type.FullName} : {type.BaseType}");
            }

            foreach (var member in type.GetMembers(Declared))
            {
                if (!IsExposed(member))
                {
                    continue;
                }

                foreach (var used in TypesOf(member))
                {
                    if (IsVendor(used))
                    {
                        offenders.Add($"{type.FullName}.{member.Name} : {used}");
                    }
                }
            }
        }

        Assert.Empty(offenders);
    }

    static bool IsExposed(MemberInfo member)
    {
        return member switch
        {
            MethodBase m => m.IsPublic || m.IsFamily || m.IsFamilyOrAssembly,
            FieldInfo f => f.IsPublic || f.IsFamily || f.IsFamilyOrAssembly,
            PropertyInfo p => (p.GetMethod?.IsPublic ?? false) || (p.SetMethod?.IsPublic ?? false),
            _ => false,
        };
    }

    static IEnumerable<Type> TypesOf(MemberInfo member)
    {
        return member switch
        {
            MethodInfo m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType),
            ConstructorInfo c => c.GetParameters().Select(p => p.ParameterType),
            FieldInfo f => [f.FieldType],
            PropertyInfo p => [p.PropertyType],
            _ => [],
        };
    }

    static bool IsVendor(Type? type)
    {
        while (type is { HasElementType: true })
        {
            type = type.GetElementType();
        }

        return type is not null && (type.Namespace?.StartsWith("MudBlazor", StringComparison.Ordinal) ?? false);
    }
}
