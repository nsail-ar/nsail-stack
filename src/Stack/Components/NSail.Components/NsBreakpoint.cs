// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>The container width from which a control's text is drawn, and the only thing that
/// decides whether it is drawn at all: Always pins the word on, Xs…Xxl hand it over from that
/// width up and take it back below, Never means there is no width at which it shows — the
/// control is its glyph, with the word left as its name (tooltip and accessible name). Its own
/// enum rather than NsSize because the two ends are not sizes: a size has no "always" and no
/// "never", and reading a width where an intention was meant is what a second flag would
/// invite.</summary>
public enum NsBreakpoint
{
    Always,
    Xs,
    Sm,
    Md,
    Lg,
    Xl,
    Xxl,
    Never
}
