// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Storage.Providers;

// The stream and what the caller needs to hand it on, never the bytes: a get answers an open
// read the caller copies wherever it is going and disposes. Length is what the target reported
// and may be -1 where it reported nothing, which is not the same as an empty object.
public sealed class ObjectContent : IDisposable
{
    public required Stream Content { get; init; }

    public long Length { get; init; } = -1;

    public string? ContentType { get; init; }

    public void Dispose()
    {
        Content.Dispose();
    }
}
