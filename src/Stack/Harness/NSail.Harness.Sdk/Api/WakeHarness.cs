// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;

namespace NSail.Harness.Api;

// Reads the board now instead of at the next poll.
[Http(Post, "api/harness/wake")]
public class WakeHarness : IMessage
{
}
