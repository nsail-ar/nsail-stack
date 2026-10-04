// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

// One column's frame: the day it stands for, mapped onto its height. NsTimeGrid builds one per
// column and cascades it; a block reads it to learn whether it belongs to this column at all
// and, if it does, where in it.
sealed class TimeGridScale
{
    public required DateOnly Date { get; init; }

    // Hours past midnight rather than pixels: the block turns this into a calc() against the
    // stylesheet's hour unit, so how tall an hour is stays in ns-mud.css and is never restated
    // in C#. Midnight is the origin because the track is the whole day — an offset measured
    // from a window's start goes negative the moment the window moves off an early block.
    public double Offset(DateTime starts)
    {
        return starts.TimeOfDay.TotalHours;
    }

    public bool Holds(DateTime starts)
    {
        return DateOnly.FromDateTime(starts) == Date;
    }
}
