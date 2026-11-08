// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Storage;

// What the settings screen may point a store at. The install decides, because a business that can
// write settings is not the one who knows which directories and hosts the server may reach.
// Nothing configured is the strict default: no directory is writable by settings, https only,
// and no loopback, link-local or private address.
public class StorageGuardOptions
{
    // Directories a `file` container must sit under (or be). Empty: settings may not name a
    // `file` container at all.
    public string[] FileRoots { get; set; } = [];

    public string[] AllowedSchemes { get; set; } = ["https"];

    // When set, an endpoint's host must be one of these (or a subdomain of one).
    public string[] AllowedHosts { get; set; } = [];

    // For an install whose store legitimately lives on its own network (a MinIO next door).
    public bool AllowPrivateAddresses { get; set; }
}
