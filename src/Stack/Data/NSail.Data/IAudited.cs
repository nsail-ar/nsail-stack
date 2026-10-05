// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Data;

/// <summary>An entity whose two audit columns are the persistence layer's to write, not the
/// handler's: <c>CreatedAt</c> when the row is added, <c>UpdatedAt</c> when it is added and every
/// time a save changes it. A handler never assigns either; a value a caller set before the add
/// (an import, a seed) is kept.</summary>
public interface IAudited
{
    DateTime CreatedAt { get; set; }

    DateTime UpdatedAt { get; set; }
}
