// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

[Flags]
public enum RunOptions
{
    None = 0,

    /// <summary>
    /// Do not set SurfaceContext.HasWork to true while the action is running.
    /// </summary>
    Background = 1,

    /// <summary>
    /// Cancel any in-flight run before starting a new one.
    /// </summary>
    Replace = 2
}
