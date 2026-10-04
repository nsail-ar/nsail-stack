// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;

namespace NSail.Data;

/// <summary>
/// A module's slice of the EF model. Kits register implementations
/// (TryAddEnumerable) and the product's DbContext applies them all.
/// </summary>
public interface IDbContextSetup
{
    void Configure(ModelBuilder modelBuilder);
}
