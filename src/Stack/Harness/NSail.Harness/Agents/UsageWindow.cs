// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Agents;

// One of the agent account's quota windows as its last run reported it: how much of it is used
// (0..1), when it resets, and how long the window is.
public sealed record UsageWindow(string Name, double Utilization, DateTimeOffset ResetsAt, TimeSpan Length)
{
    // The share used over the share of the window gone by: 1 spends it exactly by the reset.
    // Early in a window the share gone by is tiny and the pace means little, so it counts from 1%.
    public double PaceAt(DateTimeOffset now)
    {
        var left = (ResetsAt - now).TotalSeconds / Length.TotalSeconds;
        var gone = Math.Clamp(1 - left, 0.01, 1);
        return Utilization / gone;
    }
}
