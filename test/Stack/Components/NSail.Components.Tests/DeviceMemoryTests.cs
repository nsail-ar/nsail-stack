// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.JSInterop;
using NSail.Components.Tests.Fixtures;

namespace NSail.Components.Tests;

/// <summary>The device-memory seam (nsail#445): what this machine remembers, under the
/// caller's own key. Every way a read can fail — never written, unparsable, a machine that
/// refuses to store at all — answers the default, because every caller's fallback is what it
/// would have done with no memory.</summary>
public sealed class DeviceMemoryTests
{
    const string Key = "point-of-sale";

    readonly FakeDeviceStorage _storage = new();

    [Fact]
    public async Task A_remembered_value_comes_back()
    {
        var memory = new DeviceMemory(_storage);
        var drawer = Guid.NewGuid();

        await memory.Remember(Key, drawer);

        Assert.Equal(drawer, await memory.Recall<Guid?>(Key));
    }

    [Fact]
    public async Task The_key_is_the_callers_own()
    {
        var memory = new DeviceMemory(_storage);

        await memory.Remember(Key, Guid.NewGuid());

        Assert.Equal([Key], _storage.Items.Keys);
        Assert.Null(await memory.Recall<Guid?>("something-else"));
    }

    [Fact]
    public async Task A_machine_that_never_wrote_recalls_nothing()
    {
        Assert.Null(await new DeviceMemory(_storage).Recall<Guid?>(Key));
    }

    [Fact]
    public async Task A_value_that_no_longer_parses_recalls_nothing()
    {
        _storage.Write(Key, "\"not-a-guid\"");

        Assert.Null(await new DeviceMemory(_storage).Recall<Guid?>(Key));
    }

    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task A_machine_that_cannot_remember_recalls_nothing_and_does_not_throw(Exception refusal)
    {
        _storage.Refusal = refusal;

        var memory = new DeviceMemory(_storage);

        Assert.Null(await memory.Recall<Guid?>(Key));

        await memory.Remember(Key, Guid.NewGuid());
    }

    public static TheoryData<Exception> Refusals()
    {
        return new()
        {
            // How a prerendering renderer refuses interop, and how a browser refuses storage.
            new InvalidOperationException("JavaScript interop calls cannot be issued during prerendering."),
            new JSException("localStorage is not available."),
        };
    }
}
