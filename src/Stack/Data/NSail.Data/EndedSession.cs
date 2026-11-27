// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Npgsql;

namespace NSail.Data;

// A restart or an administrator's terminate ends the sessions a pool holds idle, and the pool
// hands them out unchecked: the first command on a rented connection is what learns it.
static class EndedSession
{
    // The server ended the session (57P01 an administrator's terminate, 57P02 a crash), or the
    // socket under it is already closed. Not NpgsqlException.IsTransient: that also answers a
    // timeout, which is a sick server and not a dead session.
    public static bool Is(Exception exception)
    {
        return exception switch
        {
            PostgresException postgres => postgres.SqlState is PostgresErrorCodes.AdminShutdown or PostgresErrorCodes.CrashShutdown,
            NpgsqlException npgsql => npgsql.InnerException is IOException or System.Net.Sockets.SocketException,
            _ => false,
        };
    }
}
