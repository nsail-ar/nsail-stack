// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging.Runtime.Context;

/// <summary>The header names the Stack itself puts on a delivery.</summary>
public static class MessageHeaders
{
    /// <summary>Which control asked for the operation this event belongs to — a token a
    /// lookup mints, carries in its create href and recognizes on the save that comes back,
    /// so a second lookup listening for the same event does not adopt a row nobody asked it
    /// for. The create page holds it under the same name in its own query.</summary>
    public const string Source = "source";
}
