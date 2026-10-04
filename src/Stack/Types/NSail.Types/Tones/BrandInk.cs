// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Tones;

// The two inks a chrome carries: the text drawn on the appbar and the drawer, and the
// quieter one its icons and its secondary lines wear.
public readonly record struct BrandInk(string Primary, string Secondary);
