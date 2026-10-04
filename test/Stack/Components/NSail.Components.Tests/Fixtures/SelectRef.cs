// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.References;

namespace NSail.Components.Tests.Fixtures;

public sealed record SelectRef(Guid Id, string DisplayName) : IRef;
