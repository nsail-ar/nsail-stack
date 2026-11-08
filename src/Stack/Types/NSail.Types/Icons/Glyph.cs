// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.Json;
using System.Text.Json.Serialization;

namespace NSail.Icons;

/// <summary>One entry of an icon catalog (NsIcons, or a product's own): the SVG markup a
/// control draws. It is a type and not a string so a component parameter cannot be handed
/// quoted text — Razor compiles a quoted attribute value as C# for every parameter type
/// except string, which turns a forgotten '@' into a compile error instead of a literal
/// that renders itself.</summary>
[JsonConverter(typeof(GlyphJsonConverter))]
public readonly record struct Glyph
{
    /// <summary>Always what <see cref="SvgSanitizer"/> keeps: every control draws it as raw
    /// markup, so a value that came off the wire or out of a store is inert at every site.</summary>
    public string Markup { get; }

    /// <summary>True when the value handed in held something outside the allowlist that was
    /// dropped. <see cref="Markup"/> is inert either way; a store that refuses rather than
    /// cleans (NavMenuArrangementValidator) reads this, because it only ever sees the Glyph.</summary>
    [JsonIgnore]
    public bool Altered { get; }

    public Glyph(string markup)
    {
        Markup = SvgSanitizer.Sanitize(markup, out var clean);
        Altered = !clean;
    }

    public override string ToString()
    {
        return Markup;
    }
}

// The wire shape is the markup itself: an icon crosses HTTP inside Sdk projections, and a
// record struct would otherwise serialize as an object and change every payload.
sealed class GlyphJsonConverter : JsonConverter<Glyph>
{
    public override Glyph Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new Glyph(reader.GetString() ?? string.Empty);
    }

    public override void Write(Utf8JsonWriter writer, Glyph value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Markup);
    }
}
