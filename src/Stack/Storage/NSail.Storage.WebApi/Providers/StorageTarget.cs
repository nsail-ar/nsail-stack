// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Storage.Providers;

// Where the install stores, unsealed: the one shape every provider reads, and deliberately
// vendor-free — an address, the place inside it, and the pair that opens it. A provider takes
// what it needs and ignores the rest, which is why there is one target instead of a settings
// type per provider the screen would have to grow a form for.
public sealed record StorageTarget(
    string Provider,
    string Endpoint,
    string Region,
    string Container,
    string AccessKey,
    string SecretKey)
{
    public static readonly StorageTarget None = new("", "", "", "", "", "");

    public bool IsConfigured
    {
        get { return Provider.Length > 0 && Container.Length > 0; }
    }
}
