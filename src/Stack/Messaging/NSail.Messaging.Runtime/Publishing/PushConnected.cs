// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging.Runtime.Publishing;

/// <summary>The push is listening — for the first time, or again after a drop. Whatever the
/// server published before this moment was not heard here, so whoever keeps a screen current
/// through a pushed event asks again on this one: the catch-up is the listener's own read,
/// never a replay.</summary>
public sealed class PushConnected : IMessage
{
}
