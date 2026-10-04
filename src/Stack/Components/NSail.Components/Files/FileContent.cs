// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>A picked file, read into memory, in the only vocabulary a store needs: what it
/// is called, what it is, and its bytes. Deliberately not the browser's IBrowserFile — a
/// store is asked to save a file, not to operate a stream whose read window the caller
/// already spent.</summary>
public sealed record FileContent(string Name, string ContentType, byte[] Content);
