// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Configuration;
using NSail.Configuration;
using NSail.Storage.FileSystem;
using NSail.Storage.S3;

namespace NSail.Storage.Tests;

// The install's default as the host loads it: through Load<T>, so a half-named target stops the
// host at startup instead of refusing every tenant's first upload.
public sealed class StorageOptionsTests
{
    [Fact]
    public void RefusesAtStartupAProviderNamedWithoutAContainer()
    {
        var configuration = Configuration(new() { ["Storage:Provider"] = S3StorageProvider.ProviderType });

        var refusal = Assert.Throws<InvalidOperationException>(() => configuration.Load<StorageOptions>());

        Assert.Contains("Storage.Container", refusal.Message);
    }

    [Fact]
    public void LoadsANamedTarget()
    {
        var configuration = Configuration(new()
        {
            ["Storage:Provider"] = S3StorageProvider.ProviderType,
            ["Storage:Container"] = "backups",
        });

        var target = configuration.Load<StorageOptions>().Target();

        Assert.True(target.IsConfigured);
        Assert.Equal("backups", target.Container);
    }

    [Fact]
    public void FallsToTheDiskFloorWhenTheSectionNamesNothing()
    {
        var install = Configuration([]).Load<StorageOptions>().OnDisk("/data/storage");

        Assert.Equal(FileSystemStorageProvider.ProviderType, install.Provider);
        Assert.Equal("/data/storage", install.Container);
    }

    [Fact]
    public void KeepsTheProviderTheInstallNamed()
    {
        var configuration = Configuration(new()
        {
            ["Storage:Provider"] = S3StorageProvider.ProviderType,
            ["Storage:Container"] = "backups",
        });

        var install = configuration.Load<StorageOptions>().OnDisk("/data/storage");

        Assert.Equal(S3StorageProvider.ProviderType, install.Provider);
        Assert.Equal("backups", install.Container);
    }

    static IConfiguration Configuration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
