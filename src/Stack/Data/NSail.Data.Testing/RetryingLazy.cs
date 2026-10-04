// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Data.Testing;

// Lazy<Task> caches whatever its factory produces the moment it is first evaluated, fault
// included — so one transient failure (a Postgres connection blip during the startup
// sweep) poisons every awaiter for the rest of the process, long after the condition that
// caused it is gone. This keeps Lazy's promise for the success case — one factory run,
// shared by every caller — but never caches a faulted attempt: the next accessor after a
// fault starts over instead of rethrowing history.
public sealed class RetryingLazy
{
    readonly Func<Task> _factory;
    readonly object _gate = new();

    Task? _current;

    public RetryingLazy(Func<Task> factory)
    {
        _factory = factory;
    }

    public Task Value
    {
        get
        {
            lock (_gate)
            {
                if (_current is null || _current.IsFaulted)
                {
                    _current = Invoke();
                }

                return _current;
            }
        }
    }

    Task Invoke()
    {
        try
        {
            return _factory();
        }
        catch (Exception exception)
        {
            // A factory that throws synchronously, rather than faulting the Task it
            // returns, would otherwise escape the lock as an exception instead of a
            // value this property can return — folded into a faulted Task so both
            // failure shapes read the same to every caller.
            return Task.FromException(exception);
        }
    }
}
