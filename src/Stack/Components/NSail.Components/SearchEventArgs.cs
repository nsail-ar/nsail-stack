// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

public sealed class SearchEventArgs<TItem> : AsyncEventArgs
{
    public string? Text { get; init; }

    /// <summary>Set by the handler — typically straight from the lookup
    /// ("args.Results = await Mediator.Send(...)"); keep your own reference too if the
    /// page wants the items afterwards.</summary>
    public IReadOnlyList<TItem> Results { get; set; } = [];
}
