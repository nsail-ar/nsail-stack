// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Security;

/// <summary>The hydrated code-owned handlers, one per process: built-ins are code, so what they
/// hydrate to cannot differ between the scoped managers that read them.</summary>
public sealed class BuiltInHandlers
{
    readonly object _gate = new();
    IReadOnlyList<PolicyHandler>? _handlers;

    internal IReadOnlyList<PolicyHandler> Get(Func<IReadOnlyList<PolicyHandler>> build)
    {
        lock (_gate)
        {
            return _handlers ??= build();
        }
    }
}
