// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Data;

/// <summary>What a <c>DbContext</c> tells the model about the branches it can see, so the org
/// filter can be written once, over the whole model, and still answer per operation. It is
/// <see cref="ITenanted"/> one axis down and for the identical reason: a query filter is
/// compiled into the model, the model is built once per context TYPE, so the only value a
/// filter can read at query time is one reached through the context executing it.</summary>
public interface IHasOrgScope
{
    /// <summary>The branches this context's rows are read within — the subtree the operation
    /// entered (the organization its message names, else the session's),
    /// <see cref="OrgScope.Everywhere"/> where the work stands in no branch.</summary>
    OrgScope OrgScope { get; }
}
