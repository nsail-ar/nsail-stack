// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components.Tests.Fixtures;

/// <summary>The rows a SelectLookup reads, standing in for the server WorkshopLookup sends
/// LookupWorkshops to: the create page files one and every search and every resolve reads it
/// live, over an await, so nothing in the round trip completes synchronously the way a real
/// one never does.</summary>
public sealed class SelectStore
{
    readonly List<SelectRef> _rows = [];

    /// <summary>How long either read takes to come back. Zero is the state no slot is ever in,
    /// and the overlap between a lookup's two reads is what nsail#857 lives in.</summary>
    public TimeSpan Latency { get; set; }

    public int Searches { get; private set; }

    public void Add(SelectRef row)
    {
        ArgumentNullException.ThrowIfNull(row);

        _rows.Add(row);
    }

    public async Task<IReadOnlyList<SelectRef>> Search(string? text, CancellationToken cancellationToken)
    {
        Searches++;

        await Task.Delay(Latency, cancellationToken);

        if (string.IsNullOrWhiteSpace(text))
        {
            return [.. _rows];
        }

        return [.. _rows.Where(row => row.DisplayName.Contains(text, StringComparison.OrdinalIgnoreCase))];
    }

    public async Task<SelectRef?> Find(Guid id, CancellationToken cancellationToken)
    {
        await Task.Delay(Latency, cancellationToken);

        return _rows.FirstOrDefault(row => row.Id == id);
    }
}
