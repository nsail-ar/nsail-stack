// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Mapping;

/// <summary>Writes a source onto an entity, graph included. Generated per pair declared with
/// <c>[MapFrom]</c> on the entity; the only reflection is the compiler's.
///
/// <para>A null <paramref name="target"/> is the root's own lookup: the context loads it by the
/// source's key, and a source naming no row is a create.</para></summary>
public interface IEntityMapper<in TSource, TTarget>
    where TTarget : class
{
    Task<TTarget> Map(TSource source, TTarget? target, MapContext context, CancellationToken cancellationToken = default);
}
