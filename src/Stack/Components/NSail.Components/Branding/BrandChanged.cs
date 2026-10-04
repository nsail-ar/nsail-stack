// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;

namespace NSail.Components;

/// <summary>Announces that the active brand was changed. It carries nothing on purpose: a
/// payload would be a second source of truth competing with <see cref="IBrandProvider"/>,
/// so the only thing a subscriber can do is re-ask the provider.</summary>
public sealed record BrandChanged : IMessage;
