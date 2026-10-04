// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.TypeScriptGenerator;

sealed record SdkModel(
    IReadOnlyList<string> Sources,
    IReadOnlyList<MessageShape> Messages,
    IReadOnlyList<ModelShape> Models,
    IReadOnlyList<EnumShape> Enums);

sealed record EnumShape(string Name, string Key, IReadOnlyList<string> Values);

// Key is null on a generic definition: a key names a concept, and DataPage<T> is a shape.
sealed record ModelShape(string Name, string? Key, IReadOnlyList<string> TypeParameters, IReadOnlyList<FieldShape> Fields);

sealed record MessageShape(
    string Name,
    string Key,
    string? Area,
    string Method,
    string Path,
    string Result,
    IReadOnlyList<FieldShape> Fields);

sealed record TsType(string Expression, string Kind, bool Nullable = false, string? Enum = null, TsType? Element = null)
{
    public string Full
    {
        get { return Nullable ? $"{Expression} | null" : Expression; }
    }

    // An element that is itself a union needs its parentheses back inside an array.
    public string AsElement
    {
        get { return Nullable ? $"({Full})" : Expression; }
    }
}

sealed record FieldShape(string Name, string Key, TsType Type)
{
    public string? Binding { get; init; }

    // Absent from a request leaves the message's own default; only a member the message cannot
    // do without (required, [Required], a route token) is mandatory in the TypeScript shape.
    public bool Optional { get; init; }

    public bool Required { get; init; }

    public int? MaxLength { get; init; }

    public int? MinLength { get; init; }

    public string? Min { get; init; }

    public string? Max { get; init; }

    public string? Format { get; init; }

    public string? Pattern { get; init; }

    public IReadOnlyList<string> Codes { get; init; } = [];
}
