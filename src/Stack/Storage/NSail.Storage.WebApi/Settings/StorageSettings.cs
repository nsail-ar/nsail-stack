// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Settings;

namespace NSail.Storage.Settings;

// Storage only, and server-side only on purpose: it holds the secret keys that open this
// install's object stores, so the type is not in the Sdk the browser gets. The wire shape is
// StorageSettingsModel, which has no secret to carry.
//
// One row for every store: its own fields are the default store's target, so a row saved before
// stores had names still reads as the default, and Stores holds each other declared name's own.
[SystemSettings]
public sealed class StorageSettings : StorageTargetSettings
{
    public Dictionary<string, StorageTargetSettings> Stores { get; set; } = [];

    public StorageTargetSettings For(string name)
    {
        if (name == StorageNames.Default)
        {
            return this;
        }

        return Stores.GetValueOrDefault(name) ?? new StorageTargetSettings();
    }

    public void Set(string name, StorageTargetSettings target)
    {
        if (name != StorageNames.Default)
        {
            Stores[name] = target;

            return;
        }

        Provider = target.Provider;
        Endpoint = target.Endpoint;
        Region = target.Region;
        Container = target.Container;
        AccessKey = target.AccessKey;
        SecretKey = target.SecretKey;
        Branch = target.Branch;
        LinkTemplate = target.LinkTemplate;
    }
}

public class StorageTargetSettings
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

    public string? Branch { get; set; }

    public string? LinkTemplate { get; set; }
}
