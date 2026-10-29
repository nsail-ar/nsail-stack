// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.Problems;
using NSail.Security;
using NSail.Settings;
using NSail.Storage.FileSystem;
using NSail.Storage.Objects;
using NSail.Storage.Providers;
using NSail.Storage.S3;
using NSail.Storage.Settings;
using NSail.Storage.Settings.Models;

namespace NSail.Storage.Tests;

// Stores by name, composed the way a host composes them and resolved the way a consumer asks:
// a keyed StorageManager per declared name, the unkeyed one the default's.
public sealed class NamedStoresTests : IDisposable
{
    const string Reports = "reports";
    const string Backups = "backups";

    readonly Container _container = new();
    readonly MemorySettings _settings = new();
    readonly MemoryProvider _memory = new();

    [Fact]
    public async Task TwoStoresOnDifferentProvidersEachWriteToTheirOwn()
    {
        using var scope = Host(services =>
        {
            services.AddStore(Reports, OnDisk("reports"));
            services.AddStore(Backups, new StorageOptions { Provider = MemoryProvider.ProviderType, Container = "backups" });
        });

        await Put(scope.ServiceProvider.GetRequiredKeyedService<StorageManager>(Reports), "reports/ledger.csv");
        await Put(scope.ServiceProvider.GetRequiredKeyedService<StorageManager>(Backups), "backups/nightly.zip");

        Assert.True(File.Exists(Path.Combine(_container.Path, "reports", "reports", "ledger.csv")));
        Assert.False(File.Exists(Path.Combine(_container.Path, "default", "reports", "ledger.csv")));
        Assert.Equal(["backups:backups/nightly.zip"], _memory.Written);
    }

    [Fact]
    public async Task ANameWithNoStoreOfItsOwnWritesToTheDefault()
    {
        using var scope = Host(services => services.AddStore(Reports));

        await Put(scope.ServiceProvider.GetRequiredKeyedService<StorageManager>(Reports), "reports/ledger.csv");

        Assert.True(File.Exists(Path.Combine(_container.Path, "default", "reports", "ledger.csv")));
    }

    // The install with one store, as before stores had names: the unkeyed manager is the
    // default's, and so is the keyed one under the default name.
    [Fact]
    public async Task AnInstallWithOneStoreWritesWhereItAlwaysDid()
    {
        using var scope = Host(_ => { });

        var storage = scope.ServiceProvider.GetRequiredService<StorageManager>();

        Assert.Equal(StorageNames.Default, storage.Name);
        Assert.Equal(StorageNames.Default, scope.ServiceProvider.GetRequiredKeyedService<StorageManager>(StorageNames.Default).Name);

        await Put(storage, "logos/brand.png");

        Assert.True(File.Exists(Path.Combine(_container.Path, "default", "logos", "brand.png")));
    }

    // Most specific first: the name's own row beats the install's target for it, which beats the
    // default store's row.
    [Fact]
    public async Task TheNamesOwnRowWinsThenItsInstallTargetThenTheDefault()
    {
        using var scope = Host(services => services.AddStore(Reports, OnDisk("reports-install")));

        var credentials = scope.ServiceProvider.GetRequiredService<StorageCredentialProvider>();
        var handler = scope.ServiceProvider.GetRequiredService<StorageSettingsHandler>();

        await handler.Handle(Save(StorageNames.Default, "default-row"));

        Assert.EndsWith("reports-install", (await credentials.Get(Reports)).Container);

        await handler.Handle(Save(Reports, "reports-row"));

        Assert.EndsWith("reports-row", (await credentials.Get(Reports)).Container);
        Assert.EndsWith("default-row", (await credentials.Get(StorageNames.Default)).Container);
    }

    // The stores share one settings row, so a save of one must carry every other back untouched,
    // secret and all.
    [Fact]
    public async Task SavingOneStoreKeepsEveryOther()
    {
        using var scope = Host(services => services.AddStore(Reports));

        var handler = scope.ServiceProvider.GetRequiredService<StorageSettingsHandler>();
        var reports = Save(Reports, "reports-row");

        reports.SecretKey = "reports-secret";

        await handler.Handle(reports);
        await handler.Handle(Save(StorageNames.Default, "default-row"));

        var model = (await handler.Handle(new GetStorageSettings { Name = Reports })).Value;

        Assert.EndsWith("reports-row", model.Container);
        Assert.True(model.HasSecretKey);
        Assert.Equal("reports-secret", (await scope.ServiceProvider.GetRequiredService<StorageCredentialProvider>().Get(Reports)).SecretKey);
    }

    [Fact]
    public async Task ListsOneRowPerDeclaredNameTheDefaultFirst()
    {
        using var scope = Host(services =>
        {
            services.AddStore(Reports);
            services.AddStore(Backups, OnDisk("backups"));
            services.AddStore(Reports);
        });

        var rows = await scope.ServiceProvider.GetRequiredService<StorageSettingsHandler>().Handle(new ListStorageStores());

        Assert.Equal([StorageNames.Default, Reports, Backups], rows.Select(row => row.Name));
        Assert.Equal([false, true, false], rows.Select(row => row.UsesDefault));
        Assert.EndsWith("default", rows[1].Container);
        Assert.EndsWith("backups", rows[2].Container);
    }

    [Fact]
    public async Task RefusesANameTheHostNeverDeclared()
    {
        using var scope = Host(_ => { });

        var error = await Assert.ThrowsAsync<BusinessException>(
            () => scope.ServiceProvider.GetRequiredService<StorageSettingsHandler>().Handle(Save("elsewhere", "x")));

        Assert.Equal("StorageNotDeclared", Assert.Single(error.Issues).Source);
    }

    [Fact]
    public async Task AnS3StoreLinksTheObjectAndADirectoryLinksNothing()
    {
        using var scope = Host(services => services.AddStore(Reports, new StorageOptions
        {
            Provider = S3StorageProvider.ProviderType,
            Endpoint = "https://store.example.com/",
            Container = "reports",
        }));

        var reports = scope.ServiceProvider.GetRequiredKeyedService<StorageManager>(Reports);
        var local = scope.ServiceProvider.GetRequiredService<StorageManager>();

        Assert.Equal(
            new Uri("https://store.example.com/reports/reports/2026%20Q3.csv"),
            await reports.Link("reports/2026 Q3.csv"));
        Assert.Null(await local.Link("reports/2026 Q3.csv"));
    }

    public void Dispose()
    {
        _container.Dispose();
    }

    IServiceScope Host(Action<IServiceCollection> stores)
    {
        var services = new ServiceCollection();

        services.AddSingleton<SettingsManager>(_settings);
        services.AddSingleton(new KeyProtector("NamedStoresTests"));
        services.AddStorageWebApi(OnDisk("default"));
        services.AddFileSystemStorage();
        services.AddS3Storage();
        services.AddScoped<IStorageProvider>(_ => _memory);

        stores(services);

        return services.BuildServiceProvider().CreateScope();
    }

    StorageOptions OnDisk(string directory)
    {
        return new StorageOptions
        {
            Provider = FileSystemStorageProvider.ProviderType,
            Container = Path.Combine(_container.Path, directory),
        };
    }

    SaveStorageSettings Save(string name, string directory)
    {
        return new SaveStorageSettings
        {
            Name = name,
            Provider = FileSystemStorageProvider.ProviderType,
            Container = Path.Combine(_container.Path, directory),
        };
    }

    static async Task Put(StorageManager storage, string key)
    {
        using var content = new MemoryStream([1, 2, 3]);

        await storage.Put(key, content);
    }

    // A second provider that is not the disk, so two stores side by side are two protocols and
    // not two folders.
    sealed class MemoryProvider : IStorageProvider
    {
        public const string ProviderType = "memory";

        public List<string> Written { get; } = [];

        public string Type
        {
            get { return ProviderType; }
        }

        public string DisplayName
        {
            get { return "Memory"; }
        }

        public bool IsRemote
        {
            get { return false; }
        }

        public Task Put(StorageTarget target, string key, Stream content, string? contentType, CancellationToken cancelToken = default)
        {
            Written.Add($"{target.Container}:{key}");

            return Task.CompletedTask;
        }

        public Task<ObjectContent?> Get(StorageTarget target, string key, CancellationToken cancelToken = default)
        {
            return Task.FromResult<ObjectContent?>(null);
        }

        public Task<IReadOnlyList<StoredObject>> List(StorageTarget target, string? prefix, CancellationToken cancelToken = default)
        {
            return Task.FromResult<IReadOnlyList<StoredObject>>([]);
        }

        public Task Delete(StorageTarget target, string key, CancellationToken cancelToken = default)
        {
            return Task.CompletedTask;
        }

        public Uri? Link(StorageTarget target, string key)
        {
            return null;
        }
    }
}
