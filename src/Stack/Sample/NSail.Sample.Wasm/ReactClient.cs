// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Sample;

// The React build's own address on this host: the host serves it there and the Blazor home links
// to it, so it is written once. It lives outside the Blazor route table on purpose — the two
// clients share the API, never a router.
public static class ReactClient
{
    public const string Path = "/react";
}
