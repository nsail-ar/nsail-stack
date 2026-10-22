// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace NSail.Data;

/// <summary>The second wall: a Postgres row-level security policy per tenant-scoped table,
/// comparing the row's tenant against a setting the connection carries. The query filter is the
/// first wall and a forgotten one is a leak; under a forced policy the same forgotten filter is
/// a query that returns nothing.
///
/// <para>What is public here is a contract with two readers outside this project: a product's
/// migration, which writes the DDL into its own chain
/// (<see cref="MigrationBuilderExtensions.AddTenantPolicies"/>), and the sweep that reads a
/// migrated database back against the model.</para></summary>
public static class TenantPolicies
{
    /// <summary>The session setting the policy compares against, written on connection open
    /// from <see cref="TenancyProvider.RowKey"/>. A connection that carries nothing compares
    /// against NULL, which no row equals — the SQL half of "an unresolved scope reads
    /// nothing".</summary>
    public const string Setting = "nsail.tenant";

    /// <summary>The policy's name, one per table and the same on every table, so a sweep asks
    /// for it by name rather than reading what a policy happens to say.</summary>
    public const string Name = "nsail_tenant";

    /// <summary>The meter the wall's one instrument is emitted on. Subscribed by the host that
    /// composes both this module and the telemetry one (<c>AddBaseWebApi</c>), never by the
    /// telemetry module itself: that arrow would put EF Core behind every host that exports a
    /// span, including the ones with no store.</summary>
    public const string MeterName = "NSail.Data";

    /// <summary>Whether the wall actually stands on this install: 1 where the app's Postgres
    /// role is subject to the policies, 0 where it can bypass them and the query filter is the
    /// only wall again. A metric and not only the startup log line, because the export seam
    /// drops every string attribute off a log record and an alert has to name what is off.</summary>
    public const string Standing = "nsail.tenancy.wall";

    /// <summary>The tables that owe a policy, read off a built model: every entity the tenant
    /// pass put the column on. Asked by the <see cref="IInstallScoped"/> marker and never by the
    /// presence of the column — <see cref="TenantState"/> carries a tenant of its own and is the
    /// install's record ABOUT a customer, so <c>TenantStates</c> owes none.</summary>
    public static IReadOnlyList<string> Tables(IModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return model.GetEntityTypes()
            .Where(IsTenantScoped)
            .Select(entity => entity.GetTableName())
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>The three statements one table's wall is made of, each terminated: row-level
    /// security enabled, FORCED so the role that owns the table is subject to it too, and the
    /// policy itself over both directions — a read sees only this tenant's rows and a write
    /// cannot name another's.</summary>
    public static string Create(string table)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(table);

        var quoted = Quote(table);

        return $"""
            ALTER TABLE {quoted} ENABLE ROW LEVEL SECURITY;
            ALTER TABLE {quoted} FORCE ROW LEVEL SECURITY;
            CREATE POLICY "{Name}" ON {quoted} USING ({Predicate}) WITH CHECK ({Predicate});
            """;
    }

    public static string Drop(string table)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(table);

        var quoted = Quote(table);

        return $"""
            DROP POLICY IF EXISTS "{Name}" ON {quoted};
            ALTER TABLE {quoted} NO FORCE ROW LEVEL SECURITY;
            ALTER TABLE {quoted} DISABLE ROW LEVEL SECURITY;
            """;
    }

    // nullif before the cast, because a setting the session never wrote reads as NULL and one
    // RESET on a pooled connection's return reads as the empty string: casting '' to uuid is an
    // error, not a comparison that matches nothing.
    static string Predicate
    {
        get { return $"\"{TenantColumn.Name}\" = nullif(current_setting('{Setting}', true), '')::uuid"; }
    }

    static bool IsTenantScoped(IReadOnlyEntityType entity)
    {
        return entity.FindProperty(TenantColumn.Name) is not null
            && !typeof(IInstallScoped).IsAssignableFrom(entity.ClrType);
    }

    static string Quote(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
