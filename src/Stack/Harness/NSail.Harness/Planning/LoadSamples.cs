// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Planning;

// The machine's load as samples in time (CPU and memory, percent), kept for the longest window a
// plan may ask about. An average over a window needs samples covering at least half of it: a
// harness started a minute ago has no five-minute CPU yet, and says so (null) instead of guessing.
public sealed class LoadSamples(TimeSpan keep)
{
    readonly Lock _gate = new();
    readonly Queue<(DateTimeOffset At, double Cpu, double Memory)> _samples = new();

    public void Add(DateTimeOffset at, double cpu, double memory)
    {
        lock (_gate)
        {
            _samples.Enqueue((at, cpu, memory));
            while (_samples.Count > 0 && at - _samples.Peek().At > keep) _samples.Dequeue();
        }
    }

    public double? Cpu(DateTimeOffset now, TimeSpan over)
    {
        return Average(now, over, s => s.Cpu);
    }

    public double? Memory(DateTimeOffset now, TimeSpan over)
    {
        return Average(now, over, s => s.Memory);
    }

    double? Average(DateTimeOffset now, TimeSpan over, Func<(DateTimeOffset At, double Cpu, double Memory), double> value)
    {
        lock (_gate)
        {
            var inside = _samples.Where(s => now - s.At <= over).ToList();
            if (inside.Count == 0 || now - inside[0].At < over / 2) return null;
            return inside.Average(value);
        }
    }
}
