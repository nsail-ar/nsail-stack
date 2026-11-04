// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;

namespace NSail.Harness.Api;

[Http(Get, "api/harness/status")]
public class GetStatus : IMessage<HarnessStatus>
{
}
