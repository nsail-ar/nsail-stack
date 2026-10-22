// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore.Migrations;

namespace NSail.Data;

public static class MigrationBuilderExtensions
{
    /// <summary>Walls the named tables with the tenant policy
    /// (<see cref="TenantPolicies"/>), from a product's own migration: the DDL belongs to the
    /// chain and to nothing else, because a chain is what CI applies to an empty database and
    /// schema written by a startup pass is schema no test ever sees.
    ///
    /// <para>The tables are named rather than read off the model, and that is not a choice: a
    /// migration's own <c>TargetModel</c> is the snapshot's, whose entity types carry no CLR
    /// type, so the one law that decides which tables owe a policy — the
    /// <see cref="IInstallScoped"/> marker — cannot be asked of it. What keeps the list true is
    /// the sweep that reads the migrated database back against the real model and names what is
    /// missing.</para></summary>
    public static void AddTenantPolicies(this MigrationBuilder migrationBuilder, params string[] tables)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        ArgumentNullException.ThrowIfNull(tables);

        foreach (var table in tables)
        {
            migrationBuilder.Sql(TenantPolicies.Create(table));
        }
    }

    public static void DropTenantPolicies(this MigrationBuilder migrationBuilder, params string[] tables)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        ArgumentNullException.ThrowIfNull(tables);

        foreach (var table in tables)
        {
            migrationBuilder.Sql(TenantPolicies.Drop(table));
        }
    }
}
