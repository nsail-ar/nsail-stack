// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;
using System.ComponentModel.DataAnnotations;

namespace NSail.Harness.Api;

// A lane's build waits here for a turn, served by rank (lower first); the answer is the lease to
// give back when the build ends.
[HarnessAccess(HarnessAccess.Lease)]
[Http(Post, "api/harness/leases")]
public class AcquireBuildLease : IMessage<Guid>
{
    [MaxLength(100)]
    public required string Holder { get; set; }
    [Range(-1, 20)]
    public int Rank { get; set; } = 9;
}
