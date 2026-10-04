// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>An HTML option: a value with its display text. What selection components
/// (NsSelect) read natively when given as items.</summary>
public interface IOption
{
    object? Value { get; }

    string Text { get; }
}

public sealed record Option<T>(T Value, string Text) : IOption
{
    object? IOption.Value => Value;
}
