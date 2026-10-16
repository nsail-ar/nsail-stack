// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.BaseServices.WebApi.Tests;

/// <summary>The clock a suite hands the install instead of the machine's, so a pause window and
/// a memo's own window are reached by moving time rather than by waiting it out.</summary>
public sealed class TestClock : TimeProvider
{
    DateTimeOffset _now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    public DateTime UtcNow
    {
        get { return _now.UtcDateTime; }
    }

    public override DateTimeOffset GetUtcNow()
    {
        return _now;
    }

    public void Advance(TimeSpan span)
    {
        _now += span;
    }
}
