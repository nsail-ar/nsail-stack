// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace NSail.Data;

// The half of the second wall that is not DDL: every connection the process opens says whose
// rows it may touch, so the policies in the chain have something to compare against. Written
// here rather than before each query because a policy is evaluated on every statement and a
// statement is one place a rule can be forgotten.
//
// The value is TenancyProvider.RowKey and nothing else: the install's own key wherever the wall
// is not the column, the resolved tenant's where it is — and NOTHING where the wall IS the
// column and no tenant resolved. Leaving it unwritten is the point: the policy's comparison is
// against NULL, which no row equals, so a scope that entered no tenant reads nothing in SQL for
// the same reason it reads nothing through the filter.
//
// Npgsql resets a pooled connection's state when it is returned, so the setting cannot outlive
// the connection that carried it; ResolvedTenancyProvider.Enter refuses a second tenant on one
// scope, so it cannot outlive its scope either. Both of those rest on the connection string:
// multiplexing would hand one physical connection to two scopes, and No Reset On Close would
// return it to the pool still carrying this setting. Where the rows are a tenant's, the seam
// refuses either (Setup.AddDataAccess).
//
// The write is the first statement a pooled connection runs, so it is also the first to find
// that the Postgres session behind it ended while it sat in the pool (a server restart, a
// terminated backend). Nothing of the caller's has run yet, so the connection is opened once
// more on a fresh session and the write is made there; a second failure is the caller's. A
// failure that leaves the connection open is not a dead session and is not retried, and an open
// that fails never reaches this hook.
sealed class TenantSession : DbConnectionInterceptor
{
    const string Write = "SELECT set_config(@setting, @tenant, false)";

    readonly TenancyProvider _tenancy;

    public TenantSession(TenancyProvider tenancy)
    {
        ArgumentNullException.ThrowIfNull(tenancy);

        _tenancy = tenancy;
    }

    public static async Task Enter(DbConnection connection, Guid tenant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        try
        {
            await Execute(connection, tenant, cancellationToken);
        }
        catch (DbException) when (connection.State != ConnectionState.Open)
        {
            await connection.CloseAsync();
            await connection.OpenAsync(cancellationToken);
            await Execute(connection, tenant, cancellationToken);
        }
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        if (_tenancy.RowKey is not { } tenant)
        {
            return;
        }

        try
        {
            Execute(connection, tenant);
        }
        catch (DbException) when (connection.State != ConnectionState.Open)
        {
            connection.Close();
            connection.Open();
            Execute(connection, tenant);
        }
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (_tenancy.RowKey is not { } tenant)
        {
            return;
        }

        await Enter(connection, tenant, cancellationToken);
    }

    static async Task Execute(DbConnection connection, Guid tenant, CancellationToken cancellationToken)
    {
        await using var command = Command(connection, tenant);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    static void Execute(DbConnection connection, Guid tenant)
    {
        using var command = Command(connection, tenant);

        command.ExecuteNonQuery();
    }

    // set_config and not SET: the setting's value is a parameter here, and SET takes none. A
    // tenant key is a Guid and could not carry a quote, but the seam that writes it is not the
    // place to rely on that.
    static DbCommand Command(DbConnection connection, Guid tenant)
    {
        var command = connection.CreateCommand();

        command.CommandText = Write;

        var setting = command.CreateParameter();

        setting.ParameterName = "setting";
        setting.Value = TenantPolicies.Setting;

        var value = command.CreateParameter();

        value.ParameterName = "tenant";
        value.Value = tenant.ToString("D");

        command.Parameters.Add(setting);
        command.Parameters.Add(value);

        return command;
    }
}
