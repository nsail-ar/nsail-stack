// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Mapping;

/// <summary>Copies a source onto a persisted target, so it is asynchronous and takes a
/// tracker: reaching an entity that is not in memory is a load.</summary>
public interface IEntityMapper<in TSource, TTarget>
{
    Task<TTarget> Map(TSource source, TTarget target, TrackerContext context, CancellationToken cancellationToken);
}
