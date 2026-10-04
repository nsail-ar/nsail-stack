// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components.Tests.Fixtures;

/// <summary>A store that keeps what it was handed, so a test can ask two questions the design
/// turns on: what the field actually uploaded, and — after a clear — that it uploaded or
/// touched nothing more. The seam has no delete at all, which is the point: clearing removes
/// an id from the value and the consuming form's submit is what eventually deletes.</summary>
public sealed class RecordingFileStore : FileStore
{
    public List<FileContent> Saved { get; } = [];

    public List<Guid> Ids { get; } = [];

    public override string? GetUrl(Guid id)
    {
        return $"/files/{id:N}";
    }

    protected override Task<Guid> SaveContent(FileContent file, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();

        Saved.Add(file);
        Ids.Add(id);

        return Task.FromResult(id);
    }

    /// <summary>Seeds an id as if this store had saved it, for a field arriving at a value it
    /// did not upload itself.</summary>
    public Guid Seed(string name)
    {
        var id = Save(new FileContent(name, "image/png", [1, 2, 3])).GetAwaiter().GetResult();

        return id;
    }
}
