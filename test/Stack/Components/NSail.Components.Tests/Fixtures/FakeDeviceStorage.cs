// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.JSInterop;

namespace NSail.Components.Tests.Fixtures;

/// <summary>The browser store ns.js wraps, as a dictionary: nsapp.remember writes a string
/// under a key, nsapp.recall reads it back or answers null. Refusal is the machine that
/// cannot remember — a prerendering renderer, a browser with storage disabled.</summary>
public sealed class FakeDeviceStorage : IJSRuntime
{
    readonly Dictionary<string, string> _items = new(StringComparer.Ordinal);

    public Exception? Refusal { get; set; }

    public IReadOnlyDictionary<string, string> Items => _items;

    public void Write(string key, string value)
    {
        _items[key] = value;
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        if (Refusal is not null)
        {
            throw Refusal;
        }

        var key = (string)args![0]!;

        if (identifier == "nsapp.recall")
        {
            return ValueTask.FromResult((TValue)(object?)(_items.TryGetValue(key, out var stored) ? stored : null)!);
        }

        if (identifier == "nsapp.remember")
        {
            _items[key] = (string)args[1]!;

            return ValueTask.FromResult(default(TValue)!);
        }

        throw new InvalidOperationException($"Unexpected call to {identifier}.");
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        return InvokeAsync<TValue>(identifier, args);
    }
}
