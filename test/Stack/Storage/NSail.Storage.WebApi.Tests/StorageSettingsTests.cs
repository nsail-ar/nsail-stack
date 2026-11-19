// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSail.Problems;
using NSail.Security;
using NSail.Settings;
using NSail.Storage;
using NSail.Storage.FileSystem;
using NSail.Storage.Git;
using NSail.Storage.Objects;
using NSail.Storage.Providers;
using NSail.Storage.S3;
using NSail.Storage.Settings;
using System.Collections.Concurrent;
using System.Net;

namespace NSail.Storage.Tests;

// The screen's own circuit, end to end and with no database in it: save a target, read it back,
// press the test button. What is proven is the two things the story asks of the settings half —
// the secret is sealed at rest, and the test action really exercises the target.
public sealed class StorageSettingsTests
{
    const string Secret = "the-secret-key-nobody-should-read-back";
    const string Failed = "The storage target could not be used. The server log has the detail.";

    readonly MemorySettings _settings = new();
    readonly KeyProtector _keys = new("StorageSettingsTests");
    readonly IStorageProvider[] _providers = [new FileSystemStorageProvider()];
    readonly Recorder _log = new();

    [Fact]
    public async Task SealsTheSecretKeyAndNeverAnswersItBack()
    {
        using var container = new Container();

        var handler = Settings();

        await handler.Handle(Save(container));

        var stored = (await _settings.Read<StorageSettings>()).Value;

        Assert.NotEqual(Secret, stored.SecretKey);
        Assert.Equal(Secret, _keys.Unprotect(stored.SecretKey!));

        var model = (await handler.Handle(new GetStorageSettings { Name = StorageNames.Default })).Value;

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
            Name = StorageNames.Default,
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
    }

    /// <summary>A probe that worked answers nothing at all: it has no fact to report beyond
    /// having worked, and it leaves nothing of its own behind.</summary>
    [Fact]
    public async Task TestsTheSavedTargetAndSweepsItsOwnProbe()
    {
        using var container = new Container();

        await Settings().Handle(Save(container));

        var storage = Storage();

        await Test().Handle(new TestStorageTarget { Name = StorageNames.Default });

        Assert.Empty(await storage.List("nsail-storage-test/"));
    }

    // A failed probe is a refusal, not a Succeeded flag with a sentence in Detail: a toast would
    // fade over a form rendering the very field the reason names. This one is anchored, so the
    // screen marks Proveedor with it.
    [Fact]
    public async Task RefusesOnProveedorWhenTheInstallStoresNowhere()
    {
        var error = await Assert.ThrowsAsync<BusinessException>(
            () => Test().Handle(new TestStorageTarget { Name = StorageNames.Default }));

        var issue = Assert.Single(error.Issues);

        Assert.Equal("NoStorageConfigured", issue.Code);
        Assert.Equal(nameof(StorageSettings.Provider), issue.Source);
    }

    [Fact]
    public async Task RefusesOnProveedorWhenTheTargetNamesAProviderNobodyComposed()
    {
        await Settings().Handle(new SaveStorageSettings { Name = StorageNames.Default, Provider = "s3", Container = "somewhere" });

        var error = await Assert.ThrowsAsync<BusinessException>(
            () => Test().Handle(new TestStorageTarget { Name = StorageNames.Default }));

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

        var credentials = new StorageCredentialProvider(_settings, _keys, Stores(install));

        Assert.Equal(install.Container, (await credentials.Get(StorageNames.Default)).Container);

        await Settings().Handle(Save(container));

        Assert.Equal(container.Path, (await credentials.Get(StorageNames.Default)).Container);
    }

    // A tenant with no key must not take the reader down: the secret reads as empty and the
    // sealing side is the one that throws.
    [Fact]
    public async Task ASecretThatCannotOpenForWantOfAKeyReadsAsEmpty()
    {
        await _settings.Save(new StorageSettings
        {
            Provider = "s3",
            Container = "bucket",
            SecretKey = _keys.Protect(Secret),
        }, 0);

        var credentials = new StorageCredentialProvider(_settings, new KeyProtector(new NoKey()), Stores(null));

        var target = await credentials.Get(StorageNames.Default);

        Assert.Equal("", target.SecretKey);
        Assert.Throws<MissingEncryptionKeyException>(() => credentials.Protect(Secret));
    }

    sealed class NoKey : IEncryptionKeySource
    {
        public string? Current
        {
            get { return null; }
        }
    }

    // An install that named nothing stores nowhere, which is what keeps the refusal above
    // meaning something: the default is an opt-in and not a floor Storage invents.
    [Fact]
    public async Task AnInstallThatNamedNoDefaultStoresNowhere()
    {
        var target = await Credentials().Get(StorageNames.Default);

        Assert.False(target.IsConfigured);
        Assert.Equal(StorageTarget.None, target);
    }

    [Fact]
    public async Task RefusesToTestAStoredRowOutsideTheInstallsRoots()
    {
        await _settings.Save(new StorageSettings
        {
            Provider = FileSystemStorageProvider.ProviderType,
            Container = "/",
        }, 1);

        var error = await Assert.ThrowsAsync<BusinessException>(
            () => Test().Handle(new TestStorageTarget { Name = StorageNames.Default }));

        Assert.Equal("StorageContainerOutsideRoot", Assert.Single(error.Issues).Code);
    }

    [Fact]
    public async Task AnswersAFailedProbeWithAFixedMessage()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var blocker = Path.Combine(root, "blocker");

        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(blocker, "x");

        try
        {
            await _settings.Save(new StorageSettings
            {
                Provider = FileSystemStorageProvider.ProviderType,
                Container = blocker,
            }, 1);

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => Test().Handle(new TestStorageTarget { Name = StorageNames.Default }));

            var issue = Assert.Single(error.Issues);

            Assert.Equal(Failed, issue.Message);
            Assert.DoesNotContain(blocker, issue.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    // The far end's own words are not the person's answer: an S3 body is whatever the address
    // happens to serve, and the log is where an administrator reads it.
    [Fact]
    public async Task AnswersAStoresRefusalWithTheFixedMessageAndLogsTheStoresText()
    {
        const string Body = "<!doctype html><title>405 Not Allowed</title>";

        await _settings.Save(new StorageSettings
        {
            Provider = S3StorageProvider.ProviderType,
            Endpoint = "https://example.com",
            Container = "bucket",
            AccessKey = "an-access-key",
            SecretKey = _keys.Protect(Secret),
        }, 1);

        var refusing = new S3StorageProvider(new Refusing(HttpStatusCode.MethodNotAllowed, Body));

        var error = await Assert.ThrowsAsync<BusinessException>(
            () => Test(services => services.AddSingleton<IStorageProvider>(refusing)).Handle(new TestStorageTarget { Name = StorageNames.Default }));

        var issue = Assert.Single(error.Issues);

        Assert.Equal("StorageTestFailed", issue.Source);
        Assert.Equal(Failed, issue.Message);
        Assert.DoesNotContain(error.Issues, each => each.Arguments is { Count: > 0 });
        Assert.Contains(_log.Entries, entry => entry.Contains("405", StringComparison.Ordinal) && entry.Contains(Body, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnswersGitsRefusalWithTheFixedMessageAndLogsGitsText()
    {
        await _settings.Save(new StorageSettings
        {
            Provider = GitStorageProvider.ProviderType,
            Endpoint = "https://nsail-storage-test.invalid",
            Container = "store.git",
        }, 1);

        var error = await Assert.ThrowsAsync<BusinessException>(
            () => Test(services => services.AddGitStorage()).Handle(new TestStorageTarget { Name = StorageNames.Default }));

        var issue = Assert.Single(error.Issues);

        Assert.Equal("StorageTestFailed", issue.Source);
        Assert.Equal(Failed, issue.Message);
        Assert.DoesNotContain("nsail-storage-test.invalid", issue.Message, StringComparison.Ordinal);
        Assert.Contains(_log.Entries, entry => entry.Contains("git answered:", StringComparison.Ordinal)
            && entry.Contains("nsail-storage-test.invalid", StringComparison.Ordinal));
    }

    // The probe's own verdicts are worded here, so each answers under its own rule.
    [Fact]
    public async Task AnswersAProbeThatCannotBeReadBackWithItsOwnVerdict()
    {
        await _settings.Save(new StorageSettings { Provider = Blank.ProviderType, Container = "anywhere" }, 1);

        var error = await Assert.ThrowsAsync<BusinessException>(
            () => Test(services => services.AddSingleton<IStorageProvider>(new Blank())).Handle(new TestStorageTarget { Name = StorageNames.Default }));

        var issue = Assert.Single(error.Issues);

        Assert.Equal("StorageTestNotReadBack", issue.Source);
        Assert.Equal("The object was written but could not be read back.", issue.Message);
    }

    static StorageGuardOptions Guard()
    {
        return new StorageGuardOptions { FileRoots = [Path.GetTempPath()] };
    }

    [Fact]
    public async Task RefusesAFileContainerOutsideTheInstallsRoots()
    {
        var error = await Assert.ThrowsAsync<BusinessException>(() => Settings().Handle(new SaveStorageSettings
        {
            Name = StorageNames.Default,
            Provider = FileSystemStorageProvider.ProviderType,
            Container = "/",
        }));

        Assert.Equal("StorageContainerOutsideRoot", Assert.Single(error.Issues).Code);
    }

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("file:///etc")]
    [InlineData("ssh://example.com")]
    [InlineData("https://127.0.0.1")]
    [InlineData("https://localhost:9000")]
    [InlineData("https://169.254.169.254/latest")]
    [InlineData("https://10.0.0.5")]
    [InlineData("https://[::1]")]
    public async Task RefusesAnEndpointOffTheAllowList(string endpoint)
    {
        var error = await Assert.ThrowsAsync<BusinessException>(() => Settings().Handle(new SaveStorageSettings
        {
            Name = StorageNames.Default,
            Provider = "s3",
            Container = "bucket",
            Endpoint = endpoint,
        }));

        Assert.Equal("StorageEndpointNotAllowed", Assert.Single(error.Issues).Code);
    }

    [Fact]
    public async Task ClearsTheStoredSecretWhenTheTargetMovesAndNoNewOneIsTyped()
    {
        using var container = new Container();
        using var other = new Container();

        var handler = Settings();

        await handler.Handle(Save(container));
        await handler.Handle(new SaveStorageSettings
        {
            Name = StorageNames.Default,
            Provider = FileSystemStorageProvider.ProviderType,
            Container = other.Path,
        });

        Assert.Null((await _settings.Read<StorageSettings>()).Value.SecretKey);
    }

    StorageSettingsHandler Settings()
    {
        return new StorageSettingsHandler(_settings, Credentials(), Stores(null), _providers, new StorageTargetPolicy(Guard()));
    }

    StorageManager Storage()
    {
        return new StorageManager(StorageNames.Default, Credentials(), _providers);
    }

    // Composed the way a host composes it, since the handler asks DI for the name's manager.
    StorageTestHandler Test(Action<IServiceCollection>? compose = null)
    {
        var services = new ServiceCollection();

        services.AddSingleton<SettingsManager>(_settings);
        services.AddSingleton(_keys);
        services.AddLogging(logging => logging.AddProvider(_log));
        services.AddStorageWebApi(null, Guard());
        services.AddFileSystemStorage();
        compose?.Invoke(services);

        return services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<StorageTestHandler>();
    }

    // An install that named no default of its own, so the row is the only thing that can answer
    // and "stores nowhere" is still reachable.
    StorageCredentialProvider Credentials()
    {
        return new StorageCredentialProvider(_settings, _keys, Stores(null));
    }

    static StorageStores Stores(StorageOptions? install)
    {
        return new StorageStores([new StorageDeclaration(StorageNames.Default, install)]);
    }

    static SaveStorageSettings Save(Container container)
    {
        return new SaveStorageSettings
        {
            Name = StorageNames.Default,
            Provider = FileSystemStorageProvider.ProviderType,
            Container = container.Path,
            AccessKey = "an-access-key",
            SecretKey = Secret,
        };
    }

    // Every store answers with the same status and body, the way an address that is no S3 does.
    sealed class Refusing : HttpMessageHandler, IHttpClientFactory
    {
        readonly HttpStatusCode _status;
        readonly string _body;

        public Refusing(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(this, disposeHandler: false);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(_status) { Content = new StringContent(_body) });
        }
    }

    // A target that takes every write and keeps none of it.
    sealed class Blank : IStorageProvider
    {
        public const string ProviderType = "blank";

        public string Type
        {
            get { return ProviderType; }
        }

        public string DisplayName
        {
            get { return "Blank"; }
        }

        public bool IsRemote
        {
            get { return false; }
        }

        public Task Put(StorageTarget target, string key, Stream content, string? contentType, CancellationToken cancelToken = default)
        {
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

    sealed class Recorder : ILoggerProvider, ILogger
    {
        public ConcurrentQueue<string> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName)
        {
            return this;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Enqueue(formatter(state, exception));
        }

        public void Dispose()
        {
        }
    }
}
