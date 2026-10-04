// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>The intention an action component carries and nothing else — never behavior (a
/// value that would need its own parameter or wiring becomes a component: NsSubmit, NsClose)
/// and never a look or a size. Default is the ordinary button, grey and filled, and it is
/// this enum's default so a control with no As is already it; Main is the screen's one
/// accent; Important is the accent's soft wash for an act that is notable without being the
/// screen's; Inline is the flat rung with no box of its own — with a glyph and no word it is
/// the icon button; Danger is the destructive act. Filled ranks share one shape and one
/// shadow and differ only in the wash, so none of them is ever flat. Whether the text is
/// drawn is Breakpoint's word alone (NsBreakpoint), which is why icon-only is not a rank
/// here: every rank has one. Submit, Filter and Send are NsSubmit's own.</summary>
public enum NsAs
{
    Default,
    Main,
    Important,
    Inline,
    Submit,
    Filter,
    Send,
    Danger
}
