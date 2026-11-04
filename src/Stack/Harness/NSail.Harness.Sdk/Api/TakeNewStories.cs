// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;

namespace NSail.Harness.Api;

// Whether new stories leave the queue; off, only what is started moves.
[Http(Post, "api/harness/take-new")]
public class TakeNewStories : IMessage
{
    public required bool Take { get; set; }
}
