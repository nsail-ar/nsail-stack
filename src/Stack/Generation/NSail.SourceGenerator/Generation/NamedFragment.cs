// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.SourceGenerator.Generation;

/// <summary>What one type contributes to a registration list: its name when it is registered,
/// and the findings it drew either way.</summary>
public sealed record NamedFragment(string? Name, EquatableList<DiagnosticInfo> Diagnostics);
