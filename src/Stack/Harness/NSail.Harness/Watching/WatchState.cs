// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.Json;
using NSail.Harness.Lanes;

namespace NSail.Harness.Watching;

// How far each Watch worker has read its source, in one small file beside the job history
// (watch.json): the next pass reads from there. A file lost or unreadable reads the lookback
// again, and the stories already filed keep that from filing anything twice.
public sealed class WatchState
{
    static readonly JsonSerializerOptions Json = new();

    readonly string? _path;
    readonly Lock _gate = new();
    readonly Dictionary<string, DateTimeOffset> _readUntil;

    // No root keeps it in memory only.
    public WatchState(string? root, ILogger? log = null)
    {
        if (root is null)
        {
            _readUntil = [];
            return;
        }
        Directory.CreateDirectory(root);
        _path = Path.Combine(root, "watch.json");
        _readUntil = StateFile.Read<Dictionary<string, DateTimeOffset>>(_path, () => [], Json, log);
    }

    public DateTimeOffset? ReadUntil(string worker)
    {
        lock (_gate)
        {
            return _readUntil.TryGetValue(worker, out var at) ? at : null;
        }
    }

    // Whether the worker's next pass is due: never read, or its cursor trails now by at least `every`.
    public bool Due(string worker, TimeSpan every, DateTimeOffset now)
    {
        return ReadUntil(worker) is not { } at || now - at >= every;
    }

    public void Read(string worker, DateTimeOffset until)
    {
        lock (_gate)
        {
            _readUntil[worker] = until;
            if (_path is not null) StateFile.Write(_path, _readUntil, Json);
        }
    }
}
