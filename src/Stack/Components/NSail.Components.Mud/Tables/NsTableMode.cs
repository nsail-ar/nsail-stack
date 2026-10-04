// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>How a server-fed table advances: one page replacing the last, pages appended as
/// the reader reaches the end, or the viewport deciding between them. The server contract is
/// the same either way — the same page request, answered by the same handler.</summary>
public enum NsTableMode
{
    /// <summary>Scroll at phone width (sm and below), Page above.</summary>
    Auto,
    Page,
    Scroll
}
