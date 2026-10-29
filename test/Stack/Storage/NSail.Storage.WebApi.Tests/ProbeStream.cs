// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Storage.Tests;

// An object bigger than anything a test wants to hold, produced a chunk at a time and never
// existing whole anywhere: not seekable, no Length, and it reports how far it has got on every
// read. That last part is what makes "the kit streams" an assertion instead of a claim — the
// destination can be measured WHILE the source is still producing.
sealed class ProbeStream : Stream
{
    readonly long _total;
    readonly Action<long> _onProgress;
    long _produced;

    public ProbeStream(long total, Action<long> onProgress)
    {
        _total = total;
        _onProgress = onProgress;
    }

    public override bool CanRead
    {
        get { return true; }
    }

    public override bool CanSeek
    {
        get { return false; }
    }

    public override bool CanWrite
    {
        get { return false; }
    }

    public override long Length
    {
        get { throw new NotSupportedException(); }
    }

    public override long Position
    {
        get { return _produced; }
        set { throw new NotSupportedException(); }
    }

    // The byte at offset i is (i % 251) — prime, so a copy that dropped or reordered a chunk
    // changes the hash rather than landing on the same repeating run.
    public override int Read(byte[] buffer, int offset, int count)
    {
        var produced = (int)Math.Min(count, _total - _produced);

        for (var i = 0; i < produced; i++)
        {
            buffer[offset + i] = (byte)((_produced + i) % 251);
        }

        _produced += produced;

        _onProgress(_produced);

        return produced;
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }
}
