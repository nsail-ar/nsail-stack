// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.BclExtensions;

public sealed class DelegateDisposable : IDisposable
{
    private readonly Action _dispose;
    private bool _disposed;

    public DelegateDisposable(Action dispose)
    {
        ArgumentNullException.ThrowIfNull(dispose);

        _dispose = dispose;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _dispose();
        }
    }
}