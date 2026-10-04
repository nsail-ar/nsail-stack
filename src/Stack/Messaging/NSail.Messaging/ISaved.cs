// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging;

/// <summary>A published "something was saved" event carrying the id of the row it saved —
/// the shape every `*Saved` event in the repo already had. Naming it lets a control
/// subscribe to "my concept was created" generically instead of reaching for the id through
/// a per-entity accessor.</summary>
public interface ISaved : IMessage
{
    Guid Id { get; }
}
