// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>The way into the screen a card stands for, resolved by the host that placed the
/// card and cascaded to it. A card is handed the address and the name, never the decision:
/// which cards have a door, and whether the session may open it, is the dashboard's business
/// — the card's own markup says nothing about being on one.</summary>
/// <param name="Href">The destination's address, already resolved from the route table.</param>
/// <param name="Name">The destination's own title, which the door carries as its accessible
/// name and its tooltip.</param>
/// <param name="Target">Where the door opens, in the link vocabulary every other target is
/// written in (Surfaces.Auto, Surfaces.Main). The host resolves it, the same value for every
/// card: a dashboard card is a screen the session stays in, never one it passes through.</param>
public sealed record CardDoor(string Href, string Name, Surface Target);
