// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

// What a whole day wears when it qualifies as a whole — closed, borrowed, out of season. Color
// is the caller's own for whatever the day is; Label is what the day says about itself, read
// on the column and on hover. The grid never asks why a day is tinted: the meaning is the
// caller's, and this pair is the whole of what it hands over.
public readonly record struct NsTimeTint(string Color, string? Label = null);
