// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Text.Json;
using System.Text.Json.Serialization;

namespace NSail.Security;

public enum PolicyConstraintKind
{
    Any = 1,

    Literal = 2,

    Me = 3,

    Current = 4,

    RelatedAs = 5,

    MemberOf = 6,

    MemberOfOrDescendant = 7
}

/// <summary>A restriction on one message field: the closed vocabulary — a symbol, a
/// relation, or literal values (array = contains). Every constraint relates the value
/// to the session; anything actor-independent is validation, not policy.</summary>
[JsonConverter(typeof(PolicyConstraintConverter))]
public sealed class PolicyConstraint
{
    public PolicyConstraintKind Kind { get; init; }

    /// <summary>The relationship role code, for RelatedAs.</summary>
    public string? Role { get; init; }

    public IReadOnlyList<Guid> Values { get; init; } = [];

    /// <summary>The value a Flag field is pinned to — the one literal that is not an id.
    /// Null on every other constraint, which is what tells a flag apart from a list of ids
    /// that happens to be empty.</summary>
    public bool? Flag { get; init; }

    public static PolicyConstraint Any { get; } = new() { Kind = PolicyConstraintKind.Any };

    public static PolicyConstraint Me { get; } = new() { Kind = PolicyConstraintKind.Me };

    public static PolicyConstraint Current { get; } = new() { Kind = PolicyConstraintKind.Current };

    public static PolicyConstraint MemberOf { get; } = new() { Kind = PolicyConstraintKind.MemberOf };

    public static PolicyConstraint MemberOfOrDescendant { get; } = new() { Kind = PolicyConstraintKind.MemberOfOrDescendant };

    public static PolicyConstraint RelatedAs(string role)
    {
        return new() { Kind = PolicyConstraintKind.RelatedAs, Role = role };
    }

    public static PolicyConstraint Literal(params Guid[] values)
    {
        return new() { Kind = PolicyConstraintKind.Literal, Values = values };
    }

    /// <summary>The literal a Flag field takes: the policy says true or false, and the field
    /// is pinned to it.</summary>
    public static PolicyConstraint Literal(bool flag)
    {
        return new() { Kind = PolicyConstraintKind.Literal, Flag = flag };
    }
}

/// <summary>The self-discriminating shape a constraint takes in JSON: a symbol string
/// ("@me"), a parameterized object ({"relatedAs": "medico"}), a literal id, an array of
/// literal ids, or true/false for a flag field — the vocabulary permissions.md documents,
/// each shape told apart by the token it starts with rather than by a discriminator property
/// an author has to know about. The expanded object stays readable so rows already in a
/// Policies table keep loading, and it is what gets written back: compact in, canonical out.
///
/// The converter is attached to the type instead of to a serializer's options because a
/// policy is read from storage through PolicyDocument but travels to the client through the
/// host's own options — a shape only one of those two understood would be a grant that works
/// on one side of the wire and quietly does nothing on the other.
///
/// Every shape outside the vocabulary throws. A constraint that parses into something the
/// evaluator can never satisfy — an empty literal set, a relation with no role — is a grant
/// that silently denies, which is the failure a permission must never have; here it fails at
/// load, where quarantine reports it.</summary>
sealed class PolicyConstraintConverter : JsonConverter<PolicyConstraint>
{
    const string AnySymbol = "*";
    const string MeSymbol = "@me";
    const string CurrentSymbol = "@current";
    const string MemberOfSymbol = "@memberOf";
    const string MemberOfOrDescendantSymbol = "@memberOfOrDescendant";

    const string KindProperty = "kind";
    const string RoleProperty = "role";
    const string ValuesProperty = "values";
    const string FlagProperty = "flag";
    const string RelatedAsProperty = "relatedAs";

    // Without this, a null constraint value slips past the converter into the field
    // dictionary and only surfaces as a NullReferenceException on some later send.
    public override bool HandleNull => true;

    public override PolicyConstraint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return FromText(reader.GetString()!);

            case JsonTokenType.StartArray:
                return FromArray(ref reader);

            case JsonTokenType.StartObject:
                return FromObject(ref reader);

            case JsonTokenType.True:
            case JsonTokenType.False:
                return PolicyConstraint.Literal(reader.GetBoolean());

            default:
                throw new JsonException($"A field constraint is a symbol, a literal id, an array of literal ids, true or false, or a constraint object; a JSON {reader.TokenType} is none of those.");
        }
    }

    public override void Write(Utf8JsonWriter writer, PolicyConstraint value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStartObject();
        writer.WriteString(KindProperty, value.Kind.ToString());

        if (value.Role is { } role)
        {
            writer.WriteString(RoleProperty, role);
        }

        // A flag's canonical shape carries no values array: the two are alternative payloads
        // of the same kind, and an empty list beside the flag would read as a literal set
        // nobody chose — the one shape the reader refuses.
        if (value.Flag is { } flag)
        {
            writer.WriteBoolean(FlagProperty, flag);
            writer.WriteEndObject();

            return;
        }

        writer.WriteStartArray(ValuesProperty);

        foreach (var id in value.Values)
        {
            writer.WriteStringValue(id);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    static PolicyConstraint FromText(string text)
    {
        switch (text)
        {
            case AnySymbol:
                return PolicyConstraint.Any;

            case MeSymbol:
                return PolicyConstraint.Me;

            case CurrentSymbol:
                return PolicyConstraint.Current;

            case MemberOfSymbol:
                return PolicyConstraint.MemberOf;

            case MemberOfOrDescendantSymbol:
                return PolicyConstraint.MemberOfOrDescendant;
        }

        if (text.StartsWith('@'))
        {
            throw new JsonException($"'{text}' is not a constraint symbol. The vocabulary is {MeSymbol}, {CurrentSymbol}, {MemberOfSymbol}, {MemberOfOrDescendantSymbol} and {AnySymbol}.");
        }

        return Guid.TryParse(text, out var literal)
            ? PolicyConstraint.Literal(literal)
            : throw new JsonException($"'{text}' is neither a constraint symbol nor a literal id.");
    }

    static PolicyConstraint FromArray(ref Utf8JsonReader reader)
    {
        var values = ReadValues(ref reader);

        // Arrays hold literals only: a symbol in a list would read as an OR the evaluator
        // has no case for, and policy-level OR already covers it with a second row.
        return values.Count > 0
            ? PolicyConstraint.Literal([.. values])
            : throw new JsonException("An empty literal list constrains a field to no value at all, which denies every send instead of granting one.");
    }

    static PolicyConstraint FromObject(ref Utf8JsonReader reader)
    {
        PolicyConstraintKind? kind = null;
        string? role = null;
        string? relatedAs = null;
        bool? flag = null;
        var parameterized = false;
        List<Guid> values = [];

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var property = reader.GetString()!;

            reader.Read();

            if (property.Equals(KindProperty, StringComparison.OrdinalIgnoreCase))
            {
                kind = ReadKind(ref reader);
            }
            else if (property.Equals(RoleProperty, StringComparison.OrdinalIgnoreCase))
            {
                role = reader.GetString();
            }
            else if (property.Equals(RelatedAsProperty, StringComparison.OrdinalIgnoreCase))
            {
                relatedAs = reader.GetString();
                parameterized = true;
            }
            else if (property.Equals(ValuesProperty, StringComparison.OrdinalIgnoreCase))
            {
                values = ReadValues(ref reader);
            }
            else if (property.Equals(FlagProperty, StringComparison.OrdinalIgnoreCase))
            {
                flag = ReadFlag(ref reader);
            }
            else
            {
                throw new JsonException($"'{property}' is not part of the field constraint vocabulary.");
            }
        }

        if (kind is null && !parameterized)
        {
            throw new JsonException($"A constraint object names its kind, or the parameter of the only parameterized one ('{RelatedAsProperty}').");
        }

        return Assemble(kind ?? PolicyConstraintKind.RelatedAs, role ?? relatedAs, values, flag);
    }

    static PolicyConstraint Assemble(PolicyConstraintKind kind, string? role, List<Guid> values, bool? flag)
    {
        if (flag is { } pinned)
        {
            return kind == PolicyConstraintKind.Literal && role is null && values.Count == 0
                ? PolicyConstraint.Literal(pinned)
                : throw new JsonException($"A '{FlagProperty}' is a literal on its own: the field is pinned to true or false, and nothing else answers for it.");
        }

        if (kind == PolicyConstraintKind.RelatedAs)
        {
            if (string.IsNullOrWhiteSpace(role))
            {
                throw new JsonException($"A '{RelatedAsProperty}' constraint needs the role the relationship is held as.");
            }

            return values.Count == 0
                ? PolicyConstraint.RelatedAs(role)
                : throw new JsonException($"A '{RelatedAsProperty}' constraint carries a role, not literal values.");
        }

        if (role is not null)
        {
            throw new JsonException($"A '{kind}' constraint carries no role; only '{RelatedAsProperty}' does.");
        }

        if (kind == PolicyConstraintKind.Literal)
        {
            return values.Count > 0
                ? PolicyConstraint.Literal([.. values])
                : throw new JsonException("An empty literal list constrains a field to no value at all, which denies every send instead of granting one.");
        }

        if (values.Count > 0)
        {
            throw new JsonException($"A '{kind}' constraint resolves its value against the session; literal values beside it would be read by nothing.");
        }

        return kind switch
        {
            PolicyConstraintKind.Any => PolicyConstraint.Any,
            PolicyConstraintKind.Me => PolicyConstraint.Me,
            PolicyConstraintKind.Current => PolicyConstraint.Current,
            PolicyConstraintKind.MemberOf => PolicyConstraint.MemberOf,
            PolicyConstraintKind.MemberOfOrDescendant => PolicyConstraint.MemberOfOrDescendant,
            _ => throw new JsonException($"'{kind}' is not a field constraint kind.")
        };
    }

    static PolicyConstraintKind ReadKind(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"A constraint's '{KindProperty}' is one of the vocabulary's names.");
        }

        var text = reader.GetString()!;

        return Enum.TryParse<PolicyConstraintKind>(text, ignoreCase: true, out var kind) && Enum.IsDefined(kind)
            ? kind
            : throw new JsonException($"'{text}' is not a field constraint kind.");
    }

    static bool ReadFlag(ref Utf8JsonReader reader)
    {
        return reader.TokenType is JsonTokenType.True or JsonTokenType.False
            ? reader.GetBoolean()
            : throw new JsonException($"A constraint's '{FlagProperty}' is true or false.");
    }

    static List<Guid> ReadValues(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("A constraint's literal values are an array of ids.");
        }

        List<Guid> values = [];

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.String || !Guid.TryParse(reader.GetString(), out var value))
            {
                throw new JsonException("A literal list holds ids only — a symbol in it would read as an OR the evaluator has no case for.");
            }

            values.Add(value);
        }

        return values;
    }
}
