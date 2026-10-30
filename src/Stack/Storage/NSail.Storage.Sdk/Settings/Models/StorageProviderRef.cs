// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Storage.Settings.Models;

public class StorageProviderRef
{
    public required string Type { get; set; }

    public required string DisplayName { get; set; }

    // Whether the target lives behind an address and a credential. One flag rather than a
    // field-by-field capability list: a provider either reaches over the network — address,
    // region, key pair — or it writes where the host already is, and no third shape exists to
    // describe.
    public bool IsRemote { get; set; }

    // Whether the target is a repository, so the screen also asks for its branch and link template.
    public bool IsRepository { get; set; }
}
