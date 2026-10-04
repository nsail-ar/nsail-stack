// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Data;

/// <summary>Opt-in optimistic concurrency: an entity edited through a long-lived form carries
/// an opaque version token the provider itself maintains, and a save whose token no longer
/// matches the stored row is refused instead of overwriting it. The value is never authored,
/// never compared and never interpreted by anything above the persistence layer — it travels
/// the wire as a number and comes back the same way.</summary>
public interface IVersioned
{
    uint Version { get; set; }
}
