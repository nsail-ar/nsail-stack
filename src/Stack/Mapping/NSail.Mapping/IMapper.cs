// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Mapping;

/// <summary>Copies a source onto a target. The pair is the unit of mapping: one direction,
/// two concrete types, so a generated implementation needs no runtime type inspection.
/// Mapping onto persisted entities is <see cref="IEntityMapper{TSource, TTarget}"/>.</summary>
public interface IMapper<in TSource, TTarget>
{
    TTarget Map(TSource source, TTarget target, MapperContext context);
}
