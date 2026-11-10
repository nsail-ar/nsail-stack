// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;

namespace NSail.Harness.Api;

[HarnessAccess(HarnessAccess.Lease)]
[Http(Delete, "api/harness/leases/{id}")]
public class ReleaseBuildLease : IMessage
{
    [AsRoute]
    public required Guid Id { get; set; }
}
