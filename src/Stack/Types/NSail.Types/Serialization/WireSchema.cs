// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace NSail.Serialization;

/// <summary>The JSON Schema of a wire payload, rendered from <see cref="JsonOptions.Wire"/> so it
/// speaks the wire: enums by name, members by their wire casing.
/// A member a send would reject when absent — <c>required</c> in C#, or carrying
/// <see cref="RequiredAttribute"/>, which every send validates — is listed as required, so a
/// body authored from the schema alone passes validation.</summary>
public static class WireSchema
{
    // The schema says what a well-formed body carries, not what the deserializer tolerates: a
    // number renders as "integer", not the ["string", "integer"] the Web defaults' lenient
    // read would admit, and a non-nullable reference member as "string", not ["string", "null"].
    static readonly JsonSerializerOptions Rendering = new(JsonOptions.Wire)
    {
        NumberHandling = JsonNumberHandling.Strict,
    };

    static readonly JsonSchemaExporterOptions Options = new()
    {
        TreatNullObliviousAsNonNullable = true,
        TransformSchemaNode = Transform,
    };

    public static JsonNode For(Type payloadType)
    {
        ArgumentNullException.ThrowIfNull(payloadType);

        return Rendering.GetJsonSchemaAsNode(payloadType, Options);
    }

    static JsonNode Transform(JsonSchemaExporterContext context, JsonNode schema)
    {
        // The attribute sits on the property, but the required list belongs to the object
        // that declares it, so the type's own node is where its properties are swept.
        if (context.PropertyInfo is not null || context.TypeInfo.Kind != JsonTypeInfoKind.Object || schema is not JsonObject node)
        {
            return schema;
        }

        var required = node["required"] as JsonArray;
        var listed = (required ?? []).Select(item => item?.GetValue<string>()).ToHashSet(StringComparer.Ordinal);

        foreach (var property in context.TypeInfo.Properties)
        {
            if (property.AttributeProvider?.IsDefined(typeof(RequiredAttribute), inherit: true) == true && listed.Add(property.Name))
            {
                if (required is null)
                {
                    required = [];
                    node["required"] = required;
                }

                required.Add(property.Name);
            }
        }

        return node;
    }
}
