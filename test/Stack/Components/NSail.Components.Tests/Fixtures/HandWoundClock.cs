// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components.Tests.Fixtures;

/// <summary>The clock MudBlazor arms its debounce on — MudAutocomplete injects TimeProvider and
/// creates the interval's timer through it — wound by hand so an interval ELAPSING is a step a
/// case takes rather than a wait it hopes for, and so a timer the component disposed is visibly
/// one that never fires. Only CreateTimer is answered: the time of day stays the system's, which
/// is all anything else reads it for.</summary>
sealed class HandWoundClock : TimeProvider
{
    readonly object _gate = new();
    readonly List<Winding> _winding = [];

    TimeSpan _wound;

    /// <summary>Winds the clock past everything due inside the span, firing each timer once, in
    /// the order it was armed.</summary>
    public void Advance(TimeSpan span)
    {
        Winding[] due;

        lock (_gate)
        {
            _wound += span;
            due = [.. _winding.Where(timer => timer.IsDue(_wound))];
        }

        foreach (var timer in due)
        {
            timer.Fire();
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        lock (_gate)
        {
            var timer = new Winding(this, callback, state, _wound + dueTime);

            _winding.Add(timer);

            return timer;
        }
    }

    void Forget(Winding timer)
    {
        lock (_gate)
        {
            _winding.Remove(timer);
        }
    }

    // One shot, which is how the debounce arms it (an infinite period): what a test asks of this
    // is whether the callback ever arrives, so a fired timer is forgotten rather than rearmed.
    sealed class Winding : ITimer
    {
        readonly HandWoundClock _clock;
        readonly TimerCallback _callback;
        readonly object? _state;

        TimeSpan _due;

        public Winding(HandWoundClock clock, TimerCallback callback, object? state, TimeSpan due)
        {
            _clock = clock;
            _callback = callback;
            _state = state;
            _due = due;
        }

        public bool IsDue(TimeSpan wound)
        {
            return _due <= wound;
        }

        public void Fire()
        {
            _clock.Forget(this);

            _callback(_state);
        }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (_clock._gate)
            {
                _due = _clock._wound + dueTime;
            }

            return true;
        }

        public void Dispose()
        {
            _clock.Forget(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();

            return ValueTask.CompletedTask;
        }
    }
}
