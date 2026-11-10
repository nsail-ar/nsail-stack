// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;
using System.ComponentModel.DataAnnotations;

namespace NSail.Harness.Api;

[HarnessAccess(HarnessAccess.Open)]
[Http(Get, "api/harness/stats")]
public class GetStats : IMessage<HarnessStatsView>
{
    // 24h (by hour), 7d or 30d (by day).
    [MaxLength(8)]
    public string Range { get; set; } = "24h";
}
