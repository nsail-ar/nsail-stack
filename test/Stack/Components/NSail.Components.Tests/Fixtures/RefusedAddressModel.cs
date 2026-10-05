// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components.Tests.Fixtures;

// The Channels template setting's own shape, which is why there is no attribute here: one
// string member serves every setting kind, so the kind that is a Link cannot declare a format
// rule of its own and the field is the only thing that knows one is owed.
public sealed class RefusedAddressModel
{
    public string? Link { get; set; }
}
