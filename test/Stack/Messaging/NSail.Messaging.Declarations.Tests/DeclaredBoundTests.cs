// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;
using NSail.Messaging.Annotations;
using NSail.Messaging.Runtime.Validation;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace NSail.Messaging.Declarations.Tests;

// Covers the messages this repository ships. The products' sweep of the same name covers theirs.
public class DeclaredBoundTests
{
    private static readonly Type[] Anchors =
    [
        typeof(NSail.Storage.Settings.SaveStorageSettings),
        typeof(NSail.Storage.Settings.StorageStoreSaved),
        typeof(NSail.Harness.Api.GetStatus),
        typeof(NSail.Sample.Contacts.CreateContact),
        typeof(NSail.Sample.Contacts.ContactSaved),
        typeof(NSail.Messaging.Runtime.Publishing.PushConnected),
        typeof(NSail.Components.BrandChanged),
        typeof(NSail.Components.NavMenuCountsChanged)
    ];

    [Fact]
    public void Every_shipped_message_member_declares_a_bound()
    {
        var undeclared = new List<string>();
        var seen = new HashSet<Type>();
        var messages = Anchors.Select(a => a.Assembly).Distinct().SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract && t.GetInterfaces().Any(IsMessageInterface))
            .ToList();

        Assert.True(messages.Count >= 25, $"The sweep reads every shipped message; it found {messages.Count}.");

        foreach (var message in messages)
        {
            Walk(message, seen, undeclared);
        }

        Assert.True(undeclared.Count == 0,
            "A number or string declares its bound or [Unbounded(\"why\")]: " + string.Join(", ", undeclared));
    }

    private static void Walk(Type type, HashSet<Type> seen, List<string> undeclared)
    {
        if (!seen.Add(type))
        {
            return;
        }

        foreach (var member in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (member.GetCustomAttribute<ValidatedAttribute>(inherit: true) is not null)
            {
                Walk(ElementOf(member.PropertyType), seen, undeclared);
            }

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

    private static bool IsMessageInterface(Type type)
    {
        return type == typeof(IMessage) || type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IMessage<>);
    }

    private static Type ElementOf(Type type)
    {
        if (type.IsArray)
        {
            return type.GetElementType()!;
        }

        var enumerable = type == typeof(string)
            ? null
            : new[] { type }.Concat(type.GetInterfaces())
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        return enumerable?.GetGenericArguments()[0] ?? type;
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
