// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace NSail.Data;

// The same answer the stamp writes and the filter reads. Where it is the install's Guid.Empty —
// the column's default — EF leaves the column to the database, which writes that default.
sealed class TenantKeyGenerator : ValueGenerator<Guid>
{
    public override bool GeneratesTemporaryValues
    {
        get { return false; }
    }

    public override Guid Next(EntityEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return entry.Context is ITenanted tenanted && TenantColumn.Of(tenanted) is { } key ? key : Guid.Empty;
    }
}
