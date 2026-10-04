// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging.Runtime;

/// <summary>One message the registry knows, emitted by the Policies.Handlers generator
/// beside its PolicyHandlerFactory. It travels through DI rather than a scan so the server
/// and the WebAssembly client — which registers no handler at all — see the identical
/// set.</summary>
public sealed class MessageRegistration
{
    public MessageRegistration(Type messageType)
    {
        MessageType = messageType;
    }

    public Type MessageType { get; }
}
