// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Text.Json;

namespace NSail.Messaging.Runtime.Packs;

/// <summary>A curated, replayable list of message invocations — the one envelope every pack
/// in the tree is read through. A step names a message by its registry key and carries its
/// payload as raw JSON, so a pack is data about messaging and nothing else: what the rows
/// mean belongs to whoever ships the file.</summary>
public sealed class Pack
{
    public required string Name { get; set; }

    public string? Description { get; set; }

    public required List<PackStep> Steps { get; set; }
}

public sealed class PackStep
{
    public required string Message { get; set; }

    public required JsonElement Body { get; set; }
}
