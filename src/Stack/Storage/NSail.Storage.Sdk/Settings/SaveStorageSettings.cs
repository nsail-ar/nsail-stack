// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;
using System.ComponentModel.DataAnnotations;

namespace NSail.Storage.Settings;

[Http(Put, "api/storage/settings/{name}")]
public class SaveStorageSettings : IMessage
{
    [AsRoute]
    [MaxLength(100)]
    public required string Name { get; set; }

    [Unbounded("An opaque concurrency token, not a quantity: the provider mints it and the "
        + "caller hands it back unread, so every value it holds is one the database may "
        + "have written and the only wrong one is stale, which the save refuses.")]
    public uint Version { get; set; }

    // The provider's own Type, as LookupStorageProviders answers it. Empty means the name has no
    // target of its own: a named store then writes to the default one, and the default stores
    // wherever the install's configuration says, or nowhere.
    [MaxLength(100)]
    public string? Provider { get; set; }

    [MaxLength(500)]
    public string? Endpoint { get; set; }

    [MaxLength(100)]
    public string? Region { get; set; }

    [MaxLength(500)]
    public string? Container { get; set; }

    [MaxLength(200)]
    public string? AccessKey { get; set; }

    // Blank means keep whatever is stored — the screen never receives the current key, so it
    // cannot send it back, and a save of the container alone must not erase it.
    [MaxLength(500)]
    public string? SecretKey { get; set; }
}
