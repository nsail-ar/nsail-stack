// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Storage.Providers;

namespace NSail.Storage.Tests;

// A real directory under the machine's temp, removed when the test lets go of it: the
// filesystem provider's whole point is that it writes to a disk, so a fake disk would prove
// the wrong thing.
sealed class Container : IDisposable
{
    public Container()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"nsail-storage-{Guid.NewGuid():N}");

        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public StorageTarget Target
    {
        get { return new StorageTarget("file", "", "", Path, "", ""); }
    }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
