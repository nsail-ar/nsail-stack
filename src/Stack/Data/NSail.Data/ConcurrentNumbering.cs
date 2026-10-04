// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;

namespace NSail.Data;

/// <summary>A document number is taken by whoever saves first, so the loser retries rather than
/// holding a lock: the unique index is the guarantee and this is how a handler lives with it.</summary>
public static class ConcurrentNumbering
{
    public const int MaxAttempts = 3;

    /// <summary>Whether the save lost the race for a number it had already picked.</summary>
    // Detected by reflection, not by referencing Npgsql.EntityFrameworkCore.PostgreSQL from
    // this project: the Stack stays provider-agnostic, and the unique-violation SqlState is the
    // only fact this retry needs from the driver.
    public static bool IsUniqueViolation(DbUpdateException exception)
    {
        var inner = exception.InnerException;

        if (inner is null)
            return false;

        var sqlState = inner.GetType().GetProperty("SqlState")?.GetValue(inner) as string;

        return sqlState == "23505";
    }
}
