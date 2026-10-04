// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging.Runtime;
using NSail.Metadata;
using NSail.Security;

namespace NSail.Security.Tests;

// The generator emits a registration and a factory from the same line, so a hand-built
// manager's registry is always exactly its factories' message types.
public static class Registry
{
    public static MessageRegistry For(IEnumerable<PolicyHandlerFactory> factories)
    {
        return new MessageRegistry(new MetadataProvider(), factories.Select(factory => factory.MessageType));
    }
}
