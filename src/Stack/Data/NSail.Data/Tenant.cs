// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Data;

/// <summary>Which tenant the current work belongs to, and which wall keeps it apart from the
/// others. <see cref="None"/> is the honest answer wherever tenancy is not resolving anything —
/// an install with no tenants at all, or a path that runs before resolution — and it is a
/// value rather than a null so a caller cannot forget the case exists.
///
/// <para>A tenant is isolated by exactly one of two walls, and which one it is decides both
/// halves of this type. A tenant with a <see cref="Database"/> is the connection's:
/// its rows are the install's, because the install is one customer's. A tenant without one is
/// the column's: <see cref="RowKey"/> carries it, and every row in the shared database answers
/// to it.</para></summary>
public sealed class Tenant
{
    public static Tenant None { get; } = new();

    public string? Slug { get; private init; }

    public string? Database { get; private init; }

    /// <summary>Which tenant a row in a shared table belongs to. <see cref="Guid.Empty"/> is
    /// the install's own — what every row carries wherever the rows are not a tenant's, so
    /// the filter that compares against it matches everything without ever being absent.</summary>
    public Guid RowKey { get; private init; }

    public bool IsResolved
    {
        get { return Slug is not null; }
    }

    /// <summary>A tenant whose wall is its own database.</summary>
    public static Tenant For(string slug, string database)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(database);

        return new Tenant { Slug = slug, Database = database };
    }

    /// <summary>A tenant whose wall is the tenant column, sharing one database with the rest.</summary>
    public static Tenant For(string slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        return new Tenant { Slug = slug, RowKey = TenantKey.For(slug) };
    }
}
