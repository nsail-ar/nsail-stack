// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Settings;

/// <summary>A settings value and the version of the row it was read at — the shape a settings
/// SCREEN reads, so its save can name the row it was looking at. The version is opaque: it is
/// carried back into the save that follows and compared by the store, never read, ordered or
/// interpreted by the screen holding it. Zero means no row exists yet.</summary>
public sealed record StoredSettings<T>
    where T : class, new()
{
    public T Value { get; init; } = new();

    public uint Version { get; init; }

    // The same courtesy DataPage<T> extends to a bare list: a caller with a value and no row
    // behind it — a harness answering a screen's read, a default nobody has saved — says so by
    // handing over the value, and gets the version that means "no row".
    public static implicit operator StoredSettings<T>(T value)
    {
        return new StoredSettings<T> { Value = value };
    }
}
