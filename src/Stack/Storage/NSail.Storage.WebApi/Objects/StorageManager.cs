// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Problems;
using NSail.Storage.Providers;
using NSail.Storage.Settings;

namespace NSail.Storage.Objects;

// Storage's whole outward API: put, get, list and delete a named object, and say where one can
// be read. Nothing here knows a vendor — it resolves its store's target once and hands it to
// whichever provider answers to the target's own name.
//
// One manager per store name, resolved as a keyed service: a consumer takes
// [FromKeyedServices(TheKitsStore.Name)] StorageManager, and the unkeyed one is the default
// store's. Registered by AddStorageWebApi and AddStore, never by the generator, because the
// name is a constructor argument no container could guess.
public sealed class StorageManager
{
    readonly StorageCredentialProvider _credentials;
    readonly IEnumerable<IStorageProvider> _providers;

    public StorageManager(string name, StorageCredentialProvider credentials, IEnumerable<IStorageProvider> providers)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        _credentials = credentials;
        _providers = providers;
    }

    public string Name { get; }

    public async Task Put(string key, Stream content, string? contentType = null, CancellationToken cancelToken = default)
    {
        var (provider, target) = await Resolve(cancelToken);

        await provider.Put(target, Guard(key), content, contentType, cancelToken);
    }

    public async Task<ObjectContent?> Get(string key, CancellationToken cancelToken = default)
    {
        var (provider, target) = await Resolve(cancelToken);

        return await provider.Get(target, Guard(key), cancelToken);
    }

    public async Task<IReadOnlyList<StoredObject>> List(string? prefix = null, CancellationToken cancelToken = default)
    {
        var (provider, target) = await Resolve(cancelToken);

        return await provider.List(target, prefix, cancelToken);
    }

    public async Task Delete(string key, CancellationToken cancelToken = default)
    {
        var (provider, target) = await Resolve(cancelToken);

        await provider.Delete(target, Guard(key), cancelToken);
    }

    // The address the object can be read at, or null where the store's provider has none to give.
    // Computed, not checked: it says where the object would be, whether or not it is there.
    public async Task<Uri?> Link(string key, CancellationToken cancelToken = default)
    {
        var (provider, target) = await Resolve(cancelToken);

        return provider.Link(target, Guard(key));
    }

    // Whether a Put would get as far as the provider, asked before there is anything to put: a
    // caller that queues the writing for later (a request a job fulfils) would otherwise accept
    // the ask, read a whole database and fail on its last line. It answers false for both refusals
    // Resolve raises — an install storing nowhere and a provider no host composed — because a
    // caller at the door can do nothing different about either.
    public async Task<bool> IsConfigured(CancellationToken cancelToken = default)
    {
        var target = await _credentials.Get(Name, cancelToken);

        return target.IsConfigured && _providers.Any(candidate => candidate.Type == target.Provider);
    }

    async Task<(IStorageProvider Provider, StorageTarget Target)> Resolve(CancellationToken cancelToken = default)
    {
        var target = await _credentials.Get(Name, cancelToken);

        if (!target.IsConfigured)
        {
            throw new BusinessException(BusinessProblem.RuleViolation(
                "NoStorageConfigured",
                nameof(StorageSettings.Provider),
                "This install stores nowhere yet. Choose a storage provider and a container in Settings."));
        }

        var provider = _providers.FirstOrDefault(candidate => candidate.Type == target.Provider);

        if (provider is null)
        {
            throw new BusinessException(BusinessProblem.RuleViolation(
                "StorageProviderNotComposed",
                nameof(StorageSettings.Provider),
                $"No storage provider named '{target.Provider}' is composed in this host."));
        }

        return (provider, target);
    }

    // The one door every key comes through, so a provider that resolves keys against a path —
    // the filesystem one — cannot be handed a key that climbs out of its container. Forward
    // slashes stay: they are what an object store means by a folder, and the provider that
    // writes to a disk is the one that turns them into directories.
    public static string Guard(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var normalized = key.Trim().Replace('\\', '/').TrimStart('/');

        var isSafe = normalized.Length > 0
            && !normalized.Contains("//", StringComparison.Ordinal)
            && !normalized.Split('/').Any(segment => segment is "" or "." or "..")
            && !Path.IsPathRooted(normalized)
            && normalized.IndexOfAny(Path.GetInvalidPathChars()) < 0;

        if (!isSafe)
        {
            throw new BusinessException(BusinessProblem.RuleViolation(
                "InvalidObjectKey",
                $"'{key}' is not a valid object key.",
                new Dictionary<string, string> { ["key"] = key }));
        }

        return normalized;
    }
}
