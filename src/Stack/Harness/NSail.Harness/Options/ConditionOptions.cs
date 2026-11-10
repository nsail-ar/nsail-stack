// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness;

// Open from each Start until the next End: cron expressions (minute hour day month weekday) read in
// TimeZone. A window that crosses midnight is a Start in the evening and an End in the morning. With
// no End it is a firing: the Start a Release cuts its day from.
public sealed class ScheduleCondition
{
    public string Start { get; set; } = "";
    public string End { get; set; } = "";
    public string TimeZone { get; set; } = "UTC";
}

// A load between Above and Below (percent, either may be left out), averaged over the last Minutes:
// an instant value would flip the plan on every spike.
public sealed class LoadCondition
{
    public double? Above { get; set; }
    public double? Below { get; set; }
    public int Minutes { get; set; } = 5;

    public bool Holds(double value)
    {
        return (Above is not { } above || value > above) && (Below is not { } below || value < below);
    }
}

// An agent quota window (Claude: five_hour, seven_day): its use in percent (Above, Below) and its
// pace (PaceAbove, PaceBelow) — the share used over the share of the window gone by. A pace of 1
// spends the window exactly by its reset; under 1 leaves tokens unspent, over 1 runs out early.
public sealed class UsageCondition
{
    public string Window { get; set; } = "seven_day";
    public double? Above { get; set; }
    public double? Below { get; set; }
    public double? PaceAbove { get; set; }
    public double? PaceBelow { get; set; }
}
