// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Metadata;

/// <summary>Area/feature metadata derived from a type's namespace by MetadataProvider's
/// positional template — the namespace is the one metadata that cannot drift from
/// the code.</summary>
public sealed record TypeMetadata
{
    public string? Root { get; init; }

    public string? Area { get; init; }

    public string? Feature { get; init; }

    public string? SubFeature { get; init; }

    public string? Object { get; init; }
}
