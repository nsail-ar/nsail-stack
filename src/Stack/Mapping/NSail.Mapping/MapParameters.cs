// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Mapping;

/// <summary>What a composed collection does with the parts its source no longer carries.</summary>
public enum CompositeCollectionBehavior
{
    /// <summary>They are deleted: the target ends looking like the source.</summary>
    Merge,

    /// <summary>They stay: a source can add parts without restating the ones already there.</summary>
    Append,
}

/// <summary>What happens when the root a source names does not exist.</summary>
public enum MissingRootBehavior
{
    /// <summary>The run refuses: an update of a row that is not there is the caller's mistake.</summary>
    Throw,

    /// <summary>The root is created, so the map is an upsert.</summary>
    Create,
}

/// <summary>What happens when an aggregation names a row the store does not have.</summary>
public enum MissingAggregationBehavior
{
    /// <summary>The run refuses: a reference to nothing is the caller's mistake.</summary>
    Throw,

    /// <summary>The row is created from the source, for an import that cannot order its rows.</summary>
    Create,
}

/// <summary>The knobs a mapping run may turn. Defaults are the safe reading of a message: the
/// root is updated only if it exists, and the parts end exactly as the source says.</summary>
public sealed class MapParameters
{
    public CompositeCollectionBehavior CompositeCollectionBehavior { get; init; } = CompositeCollectionBehavior.Merge;

    public MissingRootBehavior MissingRootBehavior { get; init; } = MissingRootBehavior.Throw;

    public MissingAggregationBehavior MissingAggregationBehavior { get; init; } = MissingAggregationBehavior.Throw;
}
