// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

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
// scope, so it cannot outlive its scope either. Multiplexing — which would hand one physical
// connection to two scopes — is off and has to stay off for both to hold.
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

        await using var command = Command(connection, tenant);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        if (_tenancy.RowKey is not { } tenant)
        {
            return;
        }

        using var command = Command(connection, tenant);

        command.ExecuteNonQuery();
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
