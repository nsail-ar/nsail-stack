// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;

namespace NSail.Harness.Api;

// Starts work again, past a pause and past a usage wall. A pending restart stays: nothing new starts and it still stops once drained.
[Http(Post, "api/harness/resume")]
public class ResumeHarness : IMessage
{
}
