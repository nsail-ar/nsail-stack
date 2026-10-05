// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Mapping;

/// <summary>A mapping that cannot be carried out as declared: the source broke a rule the
/// declaration made, not a business rule (those are the handler's).</summary>
public sealed class MapperException : Exception
{
    public MapperException(string message)
        : base(message)
    {
    }
}
