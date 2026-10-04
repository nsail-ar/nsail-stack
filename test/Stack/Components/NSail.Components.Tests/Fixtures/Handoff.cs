// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace NSail.Components.Tests.Fixtures;

/// <summary>Stands in for what the prerender writes into the document and the WebAssembly
/// client reads back out of it, so a test can hand a component the same state a real
/// prerender would — and read back what the component itself persisted.</summary>
public sealed class Handoff : IPersistentComponentStateStore
{
    readonly Dictionary<string, byte[]> _restored;

    Handoff(Dictionary<string, byte[]> restored)
    {
        _restored = restored;
    }

    public ComponentStatePersistenceManager Manager { get; private set; } = default!;

    public PersistentComponentState State
    {
        get { return Manager.State; }
    }

    public IReadOnlyDictionary<string, byte[]>? Persisted { get; private set; }

    public static async Task<Handoff> Empty()
    {
        return await Restored([]);
    }

    public static async Task<Handoff> Carrying(string key, object value)
    {
        return await Restored(new Dictionary<string, byte[]>
        {
            // The camelCase web defaults are what PersistAsJson writes, so the bytes a test
            // hands over are the bytes a real prerender would have written.
            [key] = JsonSerializer.SerializeToUtf8Bytes(value, JsonSerializerOptions.Web),
        });
    }

    public static async Task<Handoff> Restored(Dictionary<string, byte[]> restored)
    {
        var handoff = new Handoff(restored)
        {
            Manager = new ComponentStatePersistenceManager(NullLogger<ComponentStatePersistenceManager>.Instance),
        };

        await handoff.Manager.RestoreStateAsync(handoff);

        return handoff;
    }

    public T? Read<T>(string key)
    {
        if (Persisted is null || !Persisted.TryGetValue(key, out var bytes))
            return default;

        return JsonSerializer.Deserialize<T>(bytes, JsonSerializerOptions.Web);
    }

    public Task<IDictionary<string, byte[]>> GetPersistedStateAsync()
    {
        return Task.FromResult<IDictionary<string, byte[]>>(_restored);
    }

    public Task PersistStateAsync(IReadOnlyDictionary<string, byte[]> state)
    {
        Persisted = state;

        return Task.CompletedTask;
    }
}
