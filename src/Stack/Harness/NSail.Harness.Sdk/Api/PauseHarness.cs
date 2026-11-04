// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;

namespace NSail.Harness.Api;

// Nothing new starts until a resume; what runs goes on.
[Http(Post, "api/harness/pause")]
public class PauseHarness : IMessage
{
    public string? Why { get; set; }
}
