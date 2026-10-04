// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>Where a picked file's bytes go and how they come back as a URL. The Stack owns
/// the upload CHROME — the four states, the clear, the reorder — and knows nothing about
/// where bytes live; a kit that stores files registers its implementation and every screen
/// stops repeating the same twenty lines. A page whose files belong to somebody other than
/// the session's organization (a patient's study, a restricted scan) hands NsFileUpload its
/// own store instead of the registered one: the ownership is the page's knowledge, not the
/// component's.</summary>
public interface IFileStore
{
    Task<Guid> Save(FileContent file, CancellationToken cancellationToken = default);

    /// <summary>Where the stored bytes are served from — what a preview img reads. Null is
    /// a real answer: a store whose bytes are gated behind a per-document audited route has
    /// no URL an id alone can build, and a field over it simply shows no image.</summary>
    string? GetUrl(Guid id);

    /// <summary>The file name for an id THIS store saved during this visit, or null. Null is
    /// the honest answer for an id that arrived from the server: no store is asked to invent
    /// a name, and a stored file's identity is carried by its preview, never by a made-up
    /// line of text.</summary>
    string? GetName(Guid id);
}
