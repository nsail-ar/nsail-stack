// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;

namespace NSail.Components.Tests.Fixtures;

/// <summary>The one saved event two SelectLookups on a screen share — PartySaved's exact
/// shape, which is what lets a médico's alta reach a paciente's field.</summary>
public sealed class SelectSaved : ISaved
{
    public Guid Id { get; set; }
}
