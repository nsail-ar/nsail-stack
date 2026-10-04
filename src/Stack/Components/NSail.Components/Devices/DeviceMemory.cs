// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.JSInterop;
using NSail.Serialization;
using System.Text.Json;

namespace NSail.Components;

/// <summary>What this machine remembers — a value kept in the browser's own storage, per
/// device and never per user: whoever signs in at that counter gets the counter's answer.
/// Keys are the caller's, explicit and shared on purpose — every screen that asks for a
/// point of sale recalls the same key, so they all offer the same drawer.
///
/// A read is silent about failure: a key never written, a browser that refuses storage, a
/// value that no longer parses into <typeparamref name="T"/> — all answer the default, so a
/// caller falls back to exactly what it would have done with no memory at all. Nothing here
/// is a secret: an id stored on a machine is still authorized on every read by the server.
///
/// JS interop cannot run while a page prerenders, so a component recalls on its first
/// interactive render, never in OnInitializedAsync.</summary>
public sealed class DeviceMemory
{
    readonly IJSRuntime _js;

    public DeviceMemory(IJSRuntime js)
    {
        ArgumentNullException.ThrowIfNull(js);

        _js = js;
    }

    public async Task<T?> Recall<T>(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            var stored = await _js.InvokeAsync<string?>("nsapp.recall", key);

            return stored is null ? default : JsonSerializer.Deserialize<T>(stored, JsonOptions.Wire);
        }
        catch (Exception failure) when (IsUnavailable(failure) || failure is JsonException)
        {
            return default;
        }
    }

    public async Task Remember<T>(string key, T value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            await _js.InvokeVoidAsync("nsapp.remember", key, JsonSerializer.Serialize(value, JsonOptions.Wire));
        }
        catch (Exception failure) when (IsUnavailable(failure))
        {
            // A machine that cannot remember still works — a write nobody can make is the
            // same as never having picked, which every caller already handles.
        }
    }

    // InvalidOperationException is how a prerendering renderer refuses interop, and JSException
    // is a browser that refuses storage (private mode, a full quota). Neither is a defect this
    // seam can answer, and both mean the same thing: this machine remembers nothing.
    static bool IsUnavailable(Exception failure)
    {
        return failure is JSException or InvalidOperationException or ObjectDisposedException;
    }
}
