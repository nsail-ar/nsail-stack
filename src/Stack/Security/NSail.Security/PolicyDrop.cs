// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Security;

/// <summary>A stored policy that is in force minus something: <paramref name="Messages"/> are
/// the keys the registry no longer knows, <paramref name="ExcusedFields"/> the constrained
/// fields no surviving message declares (<see cref="SecurityManager.Drops"/>).</summary>
public sealed record PolicyDrop(
    Guid? PolicyId,
    string? Name,
    IReadOnlyList<string> Messages,
    IReadOnlyList<string> ExcusedFields);
