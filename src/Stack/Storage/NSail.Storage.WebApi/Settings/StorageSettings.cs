// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Settings;

namespace NSail.Storage.Settings;

// Storage only, and server-side only on purpose: it holds the secret key that opens this
// install's object store, so the type is not in the Sdk the browser gets. The wire shape is
// StorageSettingsModel, which has no secret to carry.
[SystemSettings]
public sealed class StorageSettings
{
    public string? Provider { get; set; }

    public string? Endpoint { get; set; }

    public string? Region { get; set; }

    public string? Container { get; set; }

    // The public half of the pair, kept beside the sealed one: they are one credential, and
    // splitting the handling is how a save starts writing a shape the read no longer understands.
    public string? AccessKey { get; set; }

    // Sealed with the install's own key, so a settings row read straight out of the database
    // opens nothing at the far end.
    public string? SecretKey { get; set; }
}
