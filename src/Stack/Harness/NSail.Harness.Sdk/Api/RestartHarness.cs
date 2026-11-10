// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;
using System.ComponentModel.DataAnnotations;

namespace NSail.Harness.Api;

// A deploy: nothing new starts, and the harness stops by itself once what runs has ended, for its
// supervisor to start the new binary. A resume before that calls it off.
[Http(Post, "api/harness/restart")]
public class RestartHarness : IMessage
{
    [MaxLength(200)]
    public string? Why { get; set; }
}
