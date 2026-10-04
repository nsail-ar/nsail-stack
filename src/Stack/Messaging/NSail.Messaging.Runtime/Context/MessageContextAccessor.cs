// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging.Runtime.Context;

/// <summary>Reads the <see cref="MessageContext"/> of the delivery in flight — the
/// IHttpContextAccessor pattern: a subscriber or a handler that wants the headers its
/// operation was published with injects this and asks, and nothing else in the process can
/// reach the delivery's context at all. Null when the operation carried no headers.
///
/// Virtual so a test injects a fake and hands its subject the headers it wants; the Stack's
/// own implementation is written by the pipelines and by nobody else.</summary>
public class MessageContextAccessor
{
    public virtual MessageContext? Context
    {
        get { return AmbientMessageContext.Value; }
    }
}
