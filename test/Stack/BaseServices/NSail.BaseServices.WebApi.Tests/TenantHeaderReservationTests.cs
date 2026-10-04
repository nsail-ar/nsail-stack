// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging.Runtime.Context;

namespace NSail.BaseServices.WebApi.Tests;

// A delivery's headers cross a transport hop under the same X-NSail- prefix this header already
// lives in (nsail#394), so the tenant word is spoken for: a send carrying a header named Tenant
// would stamp exactly what the proxy writes, and the install would read a tenancy answer from
// whoever asked. Messaging refuses that name from both ends — the two words are held equal here,
// where the header's owner is, so renaming one without the other cannot go quiet.
public sealed class TenantHeaderReservationTests
{
    [Fact]
    public void TheHeaderThisMiddlewareReadsIsTheOneADeliveryMayNotCarry()
    {
        Assert.Equal(TenancyMiddleware.TenantHeader, WireHeaders.Tenant);
        Assert.Null(WireHeaders.Name("Tenant"));
        Assert.Null(WireHeaders.Logical(TenancyMiddleware.TenantHeader));
    }
}
