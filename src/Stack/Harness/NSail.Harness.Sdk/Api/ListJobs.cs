// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;
using System.ComponentModel.DataAnnotations;

namespace NSail.Harness.Api;

[HarnessAccess(HarnessAccess.Open)]
[Http(Get, "api/harness/jobs")]
public class ListJobs : IMessage<JobsView>
{
    // How many ended jobs come back, newest first.
    [Range(0, 1000)]
    public int Recent { get; set; } = 50;
}
