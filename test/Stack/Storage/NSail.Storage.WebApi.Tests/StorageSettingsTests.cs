// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Problems;
using NSail.Security;
using NSail.Storage;
using NSail.Storage.FileSystem;
using NSail.Storage.Objects;
using NSail.Storage.Providers;
using NSail.Storage.Settings;

namespace NSail.Storage.Tests;

// The screen's own circuit, end to end and with no database in it: save a target, read it back,
// press the test button. What is proven is the two things the story asks of the settings half —
// the secret is sealed at rest, and the test action really exercises the target.
public sealed class StorageSettingsTests
{
    const string Secret = "the-secret-key-nobody-should-read-back";

    readonly MemorySettings _settings = new();
    readonly KeyProtector _keys = new("StorageSettingsTests");
    readonly IStorageProvider[] _providers = [new FileSystemStorageProvider()];

    [Fact]
    public async Task SealsTheSecretKeyAndNeverAnswersItBack()
    {
        using var container = new Container();

        var handler = Settings();

        await handler.Handle(Save(container));

        var stored = (await _settings.Read<StorageSettings>()).Value;

        Assert.NotEqual(Secret, stored.SecretKey);
        Assert.Equal(Secret, _keys.Unprotect(stored.SecretKey!));

        var model = (await handler.Handle(new GetStorageSettings())).Value;

        Assert.True(model.HasSecretKey);
        Assert.Equal(container.Path, model.Container);
        Assert.DoesNotContain(Secret, System.Text.Json.JsonSerializer.Serialize(model), StringComparison.Ordinal);
    }

    [Fact]
    public async Task KeepsTheStoredSecretWhenTheSaveLeavesTheFieldEmpty()
    {
        using var container = new Container();

        var handler = Settings();

        await handler.Handle(Save(container));

        var sealedValue = (await _settings.Read<StorageSettings>()).Value.SecretKey;

        await handler.Handle(new SaveStorageSettings
        {
            Provider = FileSystemStorageProvider.ProviderType,
            Container = container.Path,
        });

        Assert.Equal(sealedValue, (await _settings.Read<StorageSettings>()).Value.SecretKey);
    }

    [Fact]
    public async Task OffersEveryComposedProvider()
    {
        var providers = await Settings().Handle(new LookupStorageProviders());

        var local = Assert.Single(providers);

        Assert.Equal(FileSystemStorageProvider.ProviderType, local.Type);
        Assert.False(local.IsRemote);
        Assert.NotEmpty(local.Icon.Markup);
    }

    /// <summary>A probe that worked answers nothing at all: it has no fact to report beyond
    /// having worked, and it leaves nothing of its own behind.</summary>
    [Fact]
    public async Task TestsTheSavedTargetAndSweepsItsOwnProbe()
    {
        using var container = new Container();

        await Settings().Handle(Save(container));

        var storage = Storage();

        await new StorageTestHandler(storage).Handle(new TestStorageTarget());

        Assert.Empty(await storage.List("nsail-storage-test/"));
    }

    /// <summary>nsail#2006: this used to come back as Succeeded: false with a sentence in Detail,
    /// and the screen drew it as a toast that faded — over a form rendering the very field the
    /// reason names. A failed probe is a refusal, and this one is anchored, so the screen marks
    /// Proveedor with it.</summary>
    [Fact]
    public async Task RefusesOnProveedorWhenTheInstallStoresNowhere()
    {
        var error = await Assert.ThrowsAsync<BusinessException>(
            () => new StorageTestHandler(Storage()).Handle(new TestStorageTarget()));

        var issue = Assert.Single(error.Issues);

        Assert.Equal("NoStorageConfigured", issue.Code);
        Assert.Equal(nameof(StorageSettings.Provider), issue.Source);
    }

    [Fact]
    public async Task RefusesOnProveedorWhenTheTargetNamesAProviderNobodyComposed()
    {
        await Settings().Handle(new SaveStorageSettings { Provider = "s3", Container = "somewhere" });

        var error = await Assert.ThrowsAsync<BusinessException>(
            () => new StorageTestHandler(Storage()).Handle(new TestStorageTarget()));

        var issue = Assert.Single(error.Issues);

        Assert.Equal("StorageProviderNotComposed", issue.Code);
        Assert.Equal(nameof(StorageSettings.Provider), issue.Source);
        Assert.Contains("s3", issue.Message, StringComparison.Ordinal);
    }

    // The install's own default, and the one seam it falls back at: a business that saved no
    // target writes where the install writes, and a business that saved one overrides it. Every
    // caller of StorageManager inherits both answers from here.
    [Fact]
    public async Task TheInstallsDefaultAnswersUntilTheRowNamesATargetOfItsOwn()
    {
        using var container = new Container();

        var install = new StorageOptions
        {
            Provider = FileSystemStorageProvider.ProviderType,
            Container = Path.Combine(container.Path, "install"),
        };

        var credentials = new StorageCredentialProvider(_settings, _keys, install);

        Assert.Equal(install.Container, (await credentials.Get()).Container);

        await Settings().Handle(Save(container));

        Assert.Equal(container.Path, (await credentials.Get()).Container);
    }

    // An install that named nothing stores nowhere, which is what keeps the refusal above
    // meaning something: the default is an opt-in and not a floor the kit invents.
    [Fact]
    public async Task AnInstallThatNamedNoDefaultStoresNowhere()
    {
        var target = await Credentials().Get();

        Assert.False(target.IsConfigured);
        Assert.Equal(StorageTarget.None, target);
    }

    StorageSettingsHandler Settings()
    {
        return new StorageSettingsHandler(_settings, Credentials(), _providers);
    }

    StorageManager Storage()
    {
        return new StorageManager(Credentials(), _providers);
    }

    // An install that named no default of its own, so the row is the only thing that can answer
    // and "stores nowhere" is still reachable.
    StorageCredentialProvider Credentials()
    {
        return new StorageCredentialProvider(_settings, _keys, new StorageOptions());
    }

    static SaveStorageSettings Save(Container container)
    {
        return new SaveStorageSettings
        {
            Provider = FileSystemStorageProvider.ProviderType,
            Container = container.Path,
            AccessKey = "an-access-key",
            SecretKey = Secret,
        };
    }
}
