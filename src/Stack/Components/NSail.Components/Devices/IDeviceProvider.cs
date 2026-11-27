// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>The class of machine the page is being rendered for. One question, and a device
/// question rather than a size one: "is this a phone" and "is this window narrow" diverge on
/// a phone held sideways, on a split screen, and on a desktop window dragged small, and a
/// caller choosing a touch gesture wants the first.
///
/// Synchronous, and answerable before anything is painted: the server reads it off the
/// request, and the WebAssembly client off the browser's own synchronous answer, so a caller
/// reads it on its first render instead of waiting for a window to be measured. A tablet
/// answers no — it has the room to page.</summary>
public interface IDeviceProvider
{
    bool IsPhone { get; }
}
