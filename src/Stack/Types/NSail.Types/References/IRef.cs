// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.References;

/// <summary>The invariant half of the `*Ref` family: the row's key. The display half is
/// deliberately absent — a Ref's display member is named for its own concept (DisplayName,
/// Name, Code + Name), and only the identity is the same shape everywhere. This is what
/// lets a generic control take the value out of a Ref without knowing which kit wrote
/// it.</summary>
public interface IRef
{
    Guid Id { get; }
}
