// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Linq.Expressions;

namespace NSail.Mapping;

/// <summary>A <typeparamref name="TSource"/> read as a <typeparamref name="TTarget"/>, written at
/// compile time per [MapTo]. An expression rather than a delegate, so a query selects only the
/// columns the target carries.</summary>
public interface IProjection<TSource, TTarget>
{
    Expression<Func<TSource, TTarget>> Expression { get; }
}
