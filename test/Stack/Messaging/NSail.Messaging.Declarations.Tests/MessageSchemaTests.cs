// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;
using NSail.Serialization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace NSail.Messaging.Declarations.Tests;

// Covers the messages this repository ships. The products' sweep of the same name covers theirs.
public class MessageSchemaTests
{
    [Fact]
    public void Every_shipped_message_renders_its_schema_and_numbers_speak_the_wire()
    {
        var messages = DeclaredBoundTests.ShippedMessages();
        Assert.True(messages.Count >= 25, $"The sweep reads every shipped message; it found {messages.Count}.");

        var wrong = new List<string>();
        var seen = new HashSet<Type>();
        foreach (var message in messages)
        {
            Check(message, seen, wrong);
        }

        Assert.True(wrong.Count == 0, "A schema renders long, ulong and decimal as string and any other number as a number: " + string.Join(", ", wrong));
    }

    private static void Check(Type type, HashSet<Type> seen, List<string> wrong)
    {
        if (!seen.Add(type))
        {
            return;
        }

        var schema = WireSchema.For(type);
        var properties = schema["properties"] as JsonObject ?? new JsonObject();

        foreach (var member in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (member.GetCustomAttribute<JsonIgnoreAttribute>() is not null)
            {
                continue;
            }

            var name = JsonOptions.Wire.PropertyNamingPolicy?.ConvertName(member.Name) ?? member.Name;
            if (properties[name] is not JsonObject node)
            {
                wrong.Add($"{type.FullName}.{member.Name} is absent from its schema");
                continue;
            }

            var memberType = Nullable.GetUnderlyingType(member.PropertyType) ?? member.PropertyType;
            var exact = memberType == typeof(long) || memberType == typeof(ulong) || memberType == typeof(decimal);
            var types = node["type"] switch
            {
                JsonArray array => array.Select(t => t!.GetValue<string>()).ToList(),
                JsonValue value => [value.GetValue<string>()],
                _ => new List<string>()
            };

            if (exact && !types.Contains("string") || exact && types.Any(t => t is "integer" or "number"))
            {
                wrong.Add($"{type.FullName}.{member.Name} is {string.Join("|", types)}");
            }
            else if (!exact && memberType.IsPrimitive && memberType != typeof(bool) && memberType != typeof(char)
                && (types.Contains("string") || !types.Any(t => t is "integer" or "number")))
            {
                wrong.Add($"{type.FullName}.{member.Name} is {string.Join("|", types)}");
            }

            var element = DeclaredBoundTests.ElementOf(memberType);
            if (element.IsClass && element != typeof(string) && element.Namespace?.StartsWith("System", StringComparison.Ordinal) != true)
            {
                Check(element, seen, wrong);
            }
        }
    }
}
