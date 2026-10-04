// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Data;

/// <summary>What a <c>DbContext</c> tells the model about itself so the tenant filter can be
/// written once, over the whole model, and still answer per request. A query filter is compiled
/// into the model, and the model is built once per context TYPE — so the only value a filter can
/// read at query time is one reached through the context executing it. This is that reach, and
/// it is the whole of what a product's context says about tenancy.</summary>
public interface ITenanted
{
    /// <summary>Which tenant this context's rows belong to: the resolved tenant's key where the
    /// rows are a tenant's, <see cref="Guid.Empty"/> — the install's own — wherever the wall is
    /// not the column, and <c>null</c> where the wall IS the column and no tenant resolved.
    /// Null is the answer that reads nothing and refuses to write: see
    /// <see cref="TenancyProvider.RowKey"/>.</summary>
    Guid? TenantId { get; }
}
