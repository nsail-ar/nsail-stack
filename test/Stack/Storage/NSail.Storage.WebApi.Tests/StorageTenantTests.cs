// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Data;
using NSail.Problems;
using NSail.Settings;
using NSail.Storage.FileSystem;
using NSail.Storage.Objects;
using NSail.Storage.Providers;
using NSail.Storage.Settings;
using NSail.Security;

namespace NSail.Storage.Tests;

// Where the wall is the tenant, the door itself enforces the key's tenant segment: a tenant
// cannot read, overwrite, delete or list another's objects.
public sealed class StorageTenantTests : IDisposable
{
    readonly string _directory = Path.Combine(Path.GetTempPath(), "nsail-storage-" + Guid.NewGuid().ToString("N"));
    readonly MemorySettings _settings = new();
    readonly KeyProtector _keys = new("StorageTenantTests");
    readonly IStorageProvider[] _providers = [new FileSystemStorageProvider()];

    public StorageTenantTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, true);
    }

    [Fact]
    public async Task ATenantCannotReachAnotherTenantsKey()
    {
        var a = await Manager(Tenant.For("alpha"));
        var b = await Manager(Tenant.For("beta"));
        var keyB = $"logos/{Tenant.For("beta").RowKey:N}/logo.png";

        await Put(b, keyB, 2);

        await Assert.ThrowsAsync<BusinessException>(() => a.Get(keyB));
        await Assert.ThrowsAsync<BusinessException>(() => Put(a, keyB, 1));
        await Assert.ThrowsAsync<BusinessException>(() => a.Delete(keyB));
        await Assert.ThrowsAsync<BusinessException>(() => a.Link(keyB));
        await Assert.ThrowsAsync<BusinessException>(() => Put(a, "logos/logo.png", 1));

        Assert.NotNull(await b.Get(keyB));
    }

    [Fact]
    public async Task AListStaysInsideTheTenant()
    {
        var a = await Manager(Tenant.For("alpha"));
        var b = await Manager(Tenant.For("beta"));
        var keyA = $"logos/{Tenant.For("alpha").RowKey:N}/logo.png";
        var keyB = $"logos/{Tenant.For("beta").RowKey:N}/logo.png";

        await Put(a, keyA, 1);
        await Put(b, keyB, 2);

        Assert.Equal([keyA], (await a.List()).Select(item => item.Key));
        Assert.Equal([keyA], (await a.List("logos/")).Select(item => item.Key));
        Assert.Empty(await a.List($"logos/{Tenant.For("beta").RowKey:N}/"));
    }

    [Fact]
    public async Task AScopeInNoTenantStoresNothing()
    {
        var none = await Manager(Tenant.None, TenancyScope.Tenant);

        await Assert.ThrowsAsync<BusinessException>(() => none.List());
        await Assert.ThrowsAsync<BusinessException>(() => Put(none, "logos/x/logo.png", 1));
    }

    [Fact]
    public async Task AnInstallWithoutTenancyKeepsItsKeysAsTheyWere()
    {
        var install = await Manager(Tenant.None, TenancyScope.Install);
        var key = $"logos/{Guid.Empty:N}/logo.png";

        await Put(install, key, 1);
        await Put(install, "backups/a.zip", 1);

        Assert.NotNull(await install.Get(key));
        Assert.Equal(2, (await install.List()).Count);
    }

    static async Task Put(StorageManager manager, string key, byte value)
    {
        using var content = new MemoryStream([value]);

        await manager.Put(key, content);
    }

    async Task<StorageManager> Manager(Tenant tenant, TenancyScope rows = TenancyScope.Tenant)
    {
        await _settings.Save(new StorageSettings
        {
            Provider = FileSystemStorageProvider.ProviderType,
            Container = _directory,
            AccessKey = "an-access-key",
            SecretKey = _keys.Protect("secret"),
        }, 0);

        var credentials = new StorageCredentialProvider(
            _settings,
            _keys,
            new StorageStores([new StorageDeclaration(StorageNames.Default, null)]));

        return new StorageManager(StorageNames.Default, credentials, _providers, new Scoped(rows, tenant));
    }

    sealed class Scoped(TenancyScope rows, Tenant tenant) : TenancyProvider(new TenancyOptions { Mode = rows == TenancyScope.Tenant ? TenancyMode.SingleDb : TenancyMode.None })
    {
        public override Tenant Current
        {
            get { return tenant; }
        }
    }
}
