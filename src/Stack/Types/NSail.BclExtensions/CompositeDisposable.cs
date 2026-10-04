// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.BclExtensions;

public sealed class CompositeDisposable : IDisposable
{
    readonly object _sync = new();
    List<IDisposable>? _disposables;
    bool _disposed;

    public CompositeDisposable()
    {
        _disposables = new List<IDisposable>();
    }

    public void Add(IDisposable disposable)
    {
        if (disposable is null)
        {
            return;
        }

        bool disposeNow = false;

        lock (_sync)
        {
            if (_disposed)
            {
                disposeNow = true;
            }
            else
            {
                _disposables!.Add(disposable);
            }
        }

        if (disposeNow)
        {
            disposable.Dispose();
        }
    }

    public void Dispose()
    {
        List<IDisposable>? disposables;

        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            disposables = _disposables;
            _disposables = null;
        }

        if (disposables is null)
        {
            return;
        }

        foreach (var disposable in disposables)
        {
            try
            {
                disposable.Dispose();
            }
            catch
            {
                // Intentionally ignored.
                // Dispose should never throw.
            }
        }
    }
}
