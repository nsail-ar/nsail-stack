// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.Security;

namespace NSail.Data.Testing;

public static class Sessions
{
    public static Session User(Guid partyId)
    {
        return new Session
        {
            IsAuthenticated = true,
            UserId = Guid.NewGuid(),
            PartyId = partyId,
            Identity = $"user-{partyId:N}",
            DisplayName = "Test user",
        };
    }

    // The scoped SessionProvider is the seam Iam's edge writes to once per request; a test
    // writes to the same one, so nothing downstream can tell a hand-built session from a
    // signed-in one. Setting it per scope rather than per host is what lets two sessions act
    // against one database in a single test.
    public static void SetSession(this IServiceProvider scope, Session session)
    {
        scope.GetRequiredService<SessionProvider>().Session = session;
    }
}
