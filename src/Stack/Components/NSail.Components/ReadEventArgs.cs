// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>One run of an NsLoad's OnLoad. Carries the region's own CancellationToken, which
/// the read passes to whatever it sends: a read the region has superseded is cancelled, and
/// only a read that honours the token stops costing anything at that instant.</summary>
public sealed class ReadEventArgs : AsyncEventArgs
{
}
