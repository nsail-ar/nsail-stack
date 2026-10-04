// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Npgsql;

namespace NSail.Data;

/// <summary>Completes a connection string whose password — and, when the machine says
/// so, whose host and port — was deliberately left out of the repo. Dev connection
/// strings carry no secret: the password arrives per machine in <c>PGPASSWORD</c>, and
/// a containerized run repoints the fixed <c>Host=localhost</c> with <c>PGHOST</c> and
/// <c>PGPORT</c> — the variables psql already honors, which Npgsql alone does not
/// read. An explicit <c>Password=</c> in the string always wins, so a deployed
/// install's full connection string passes through untouched.</summary>
public static class ConnectionStrings
{
    public static string Complete(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        if (!string.IsNullOrEmpty(builder.Password))
        {
            return connectionString;
        }

        var host = Environment.GetEnvironmentVariable("PGHOST");

        if (!string.IsNullOrEmpty(host))
        {
            builder.Host = host;
        }

        var port = Environment.GetEnvironmentVariable("PGPORT");

        if (!string.IsNullOrEmpty(port) && int.TryParse(port, out var portNumber))
        {
            builder.Port = portNumber;
        }

        var password = Environment.GetEnvironmentVariable("PGPASSWORD");

        if (!string.IsNullOrEmpty(password))
        {
            builder.Password = password;
        }

        return builder.ConnectionString;
    }
}
