// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>The remembering half of every store, written once: what a store saved this
/// visit is what the upload field shows on its line, and what a form that needs the name
/// beside the id reads back at submit time. Implementations supply the transport alone.</summary>
public abstract class FileStore : IFileStore
{
    readonly Dictionary<Guid, FileContent> _saved = [];

    public async Task<Guid> Save(FileContent file, CancellationToken cancellationToken = default)
    {
        var id = await SaveContent(file, cancellationToken);

        _saved[id] = file;

        return id;
    }

    public abstract string? GetUrl(Guid id);

    public string? GetName(Guid id)
    {
        return Get(id)?.Name;
    }

    /// <summary>What this store saved under an id during this visit, or null — the name and
    /// content type a document that stores them beside the id (a note's attachments) writes
    /// at submit, without a second read of anything.</summary>
    public FileContent? Get(Guid id)
    {
        return _saved.TryGetValue(id, out var file) ? file : null;
    }

    protected abstract Task<Guid> SaveContent(FileContent file, CancellationToken cancellationToken);
}
