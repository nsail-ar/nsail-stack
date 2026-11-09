// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;
using NSail.Messaging.Annotations;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace NSail.Messaging.Declarations.Tests;

// Covers the messages this repository ships. The products' sweep of the same name covers theirs.
public class DeclaredBoundTests
{
    private static readonly Type[] Anchors =
    [
        typeof(NSail.Storage.Settings.SaveStorageSettings),
        typeof(NSail.Harness.Api.GetStatus),
        typeof(NSail.Sample.Contacts.CreateContact)
    ];

    [Fact]
    public void Every_shipped_message_member_declares_a_bound()
    {
        var undeclared = new List<string>();

        foreach (var type in Anchors.Select(a => a.Assembly).Distinct().SelectMany(a => a.GetTypes()))
        {
            if (type.IsAbstract || !typeof(IMessage).IsAssignableFrom(type))
            {
                continue;
            }

            foreach (var member in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var kind = Kind(member.PropertyType);
                if (kind is null || member.GetCustomAttribute<UnboundedAttribute>() is not null)
                {
                    continue;
                }

                var bounded = kind == "number"
                    ? member.GetCustomAttributes<RangeAttribute>().Any()
                        || HasNamed(member, "NotNegativeAttribute", "NotAboveAttribute")
                    : member.GetCustomAttributes<StringLengthAttribute>().Any()
                        || member.GetCustomAttributes<MaxLengthAttribute>().Any()
                        || member.GetCustomAttributes<RegularExpressionAttribute>().Any()
                        || member.GetCustomAttributes<LengthAttribute>().Any()
                        || HasNamed(member, "EmailAddressAttribute");

                if (!bounded)
                {
                    undeclared.Add($"{type.FullName}.{member.Name}");
                }
            }
        }

        Assert.True(undeclared.Count == 0,
            "A number or string declares its bound or [Unbounded(\"why\")]: " + string.Join(", ", undeclared));
    }

    private static string? Kind(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(string))
        {
            return "string";
        }

        return type == typeof(bool) || type == typeof(char) || !type.IsPrimitive && type != typeof(decimal)
            ? null
            : "number";
    }

    private static bool HasNamed(MemberInfo member, params string[] names)
    {
        return member.GetCustomAttributes(true).Any(a => names.Contains(a.GetType().Name));
    }
}
