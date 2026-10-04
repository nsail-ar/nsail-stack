// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Text.Json;
using System.Text.Json.Serialization;

namespace NSail.Serialization;

/// <summary>The JSON shape of every NSail wire payload. Both ends of a message read this:
/// the generated client serializes with it, the host configures its own pipeline from
/// <see cref="Converters"/>.</summary>
public static class JsonOptions
{
    /// <summary>Converters the wire contract requires. A host cannot reuse <see cref="Wire"/>
    /// directly — it owns its own options instance — so it copies these.</summary>
    public static IReadOnlyList<JsonConverter> Converters { get; } = new JsonConverter[]
    {
        new JsonStringEnumConverter()
    };

    public static JsonSerializerOptions Wire { get; } = Create();

    static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        foreach (var converter in Converters)
        {
            options.Converters.Add(converter);
        }

        options.MakeReadOnly(populateMissingResolver: true);

        return options;
    }
}
