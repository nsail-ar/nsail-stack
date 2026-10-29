// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Storage.Settings.Models;

// The stored target minus the secret half: what the screen reads back is whether a secret
// key exists, never the key. A single type serving both storage and the wire would put the
// install's credential in every administrator's browser, so the two shapes are separate types
// rather than one type someone must remember to blank.
public class StorageSettingsModel
{
    public string? Provider { get; set; }

    public string? Endpoint { get; set; }

    public string? Region { get; set; }

    public string? Container { get; set; }

    public string? AccessKey { get; set; }

    public bool HasSecretKey { get; set; }
}
