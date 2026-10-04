// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;

namespace NSail.Components;

/// <summary>Something happened that the numbers beside the menu entries may disagree with, so
/// every <see cref="INavMenuCount"/> is asked again. A UI event and not an <c>[Http]</c>
/// message: it never leaves the client.
///
/// <para>It carries nothing, and that is the whole of its design. The Stack owns no count — a
/// number belongs to whichever module contributed it — so an event naming one entry, one module
/// or one id would be the drawer learning what a kit counts. "Ask again" is the only reaction
/// that is honest for every contributor at once, which is the same bargain
/// <c>BooksPosted</c> and <c>StockChanged</c> make (messaging.md).</para></summary>
public sealed class NavMenuCountsChanged : IMessage
{
}
