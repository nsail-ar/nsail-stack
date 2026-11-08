// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NSail.Serialization;

// A JSON number is a double to every JavaScript reader: past 2^53 an integer, and past 15 or
// so digits a decimal, loses its tail. These types travel as strings; reading accepts either
// form, so a client that still writes numbers keeps working. A text that does not parse is a
// JsonException, which the endpoint answers as a malformed body, not a failure of its own.
sealed class Int64StringConverter : JsonConverter<long>
{
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetInt64();
        }

        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"A {reader.TokenType} is not a number.");
        }

        return Parse(reader.GetString());
    }

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
    }

    public override long ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return Parse(reader.GetString());
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        writer.WritePropertyName(value.ToString(CultureInfo.InvariantCulture));
    }

    static long Parse(string? text)
    {
        if (long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        throw new JsonException($"'{text}' is not a long.");
    }
}

sealed class UInt64StringConverter : JsonConverter<ulong>
{
    public override ulong Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetUInt64();
        }

        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"A {reader.TokenType} is not a number.");
        }

        return Parse(reader.GetString());
    }

    public override void Write(Utf8JsonWriter writer, ulong value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
    }

    public override ulong ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return Parse(reader.GetString());
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, ulong value, JsonSerializerOptions options)
    {
        writer.WritePropertyName(value.ToString(CultureInfo.InvariantCulture));
    }

    static ulong Parse(string? text)
    {
        if (ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        throw new JsonException($"'{text}' is not a ulong.");
    }
}

sealed class DecimalStringConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetDecimal();
        }

        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"A {reader.TokenType} is not a number.");
        }

        return Parse(reader.GetString());
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
    }

    public override decimal ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return Parse(reader.GetString());
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
    {
        writer.WritePropertyName(value.ToString(CultureInfo.InvariantCulture));
    }

    static decimal Parse(string? text)
    {
        if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        throw new JsonException($"'{text}' is not a decimal.");
    }
}
