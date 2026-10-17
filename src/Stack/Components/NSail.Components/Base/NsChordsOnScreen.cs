// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>The chords claimed on screen at one instant, as the layer in ns.js reads them off
/// the DOM for the "?" sheet (<c>nsapp.chords</c>). Three named groups rather than a flat list
/// with a label on each line: the sheet's headings are the house's and in its order, and a
/// fourth group is not a thing the read can hand back.
///
/// <para>Which group a claim falls in is WHERE the declaring control stands — a grid's rows,
/// the form around them, the frame around that — so no control declares its group and none can
/// declare the wrong one.</para></summary>
public sealed record NsChordsOnScreen
{
    public IReadOnlyList<NsChordClaim> Navigation { get; init; } = [];

    public IReadOnlyList<NsChordClaim> Forms { get; init; } = [];

    public IReadOnlyList<NsChordClaim> Grids { get; init; } = [];
}

/// <summary>One line of the "?" sheet: the keys as the control published them, and that
/// control's own word for what they do — already in the install's language, because it is the
/// word on screen and not a second string to translate.</summary>
public sealed record NsChordClaim
{
    public string Keys { get; init; } = string.Empty;

    public string Act { get; init; } = string.Empty;
}
