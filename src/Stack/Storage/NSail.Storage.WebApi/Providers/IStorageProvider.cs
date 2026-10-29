// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Storage.Providers;

// The contract may change until the second real vendor lands; after the one that follows S3,
// it is frozen. One provider teaches you its own shape, not the abstraction's.
//
// Every operation takes the target rather than reading the settings itself: a provider is a
// stateless translator of these verbs into one protocol, and the store's configuration is
// resolved once, by StorageManager, for whichever provider answers.
public interface IStorageProvider
{
    string Type { get; }

    string DisplayName { get; }

    // Whether the target lives behind an address and a credential — what the settings screen
    // reads to know which fields to ask for.
    bool IsRemote { get; }

    // Streams both ways and never materializes an object: content is read from the caller's
    // stream as it is written, so an archive larger than memory costs a buffer, not a copy.
    Task Put(StorageTarget target, string key, Stream content, string? contentType, CancellationToken cancelToken = default);

    // Null for an object that is not there. The caller owns the returned content and disposes
    // it; the read is still open when this returns.
    Task<ObjectContent?> Get(StorageTarget target, string key, CancellationToken cancelToken = default);

    Task<IReadOnlyList<StoredObject>> List(StorageTarget target, string? prefix, CancellationToken cancelToken = default);

    // Deleting what is not there succeeds: the caller asked for the object to be gone.
    Task Delete(StorageTarget target, string key, CancellationToken cancelToken = default);

    // The address the object can be read at, computed from the target and the key with no call
    // to the store; null where the provider has no address to give (a directory on the host).
    // Whether a reader is let in at that address is the store's own access policy.
    Uri? Link(StorageTarget target, string key);
}
