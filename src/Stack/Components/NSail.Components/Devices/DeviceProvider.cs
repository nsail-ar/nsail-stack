// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace NSail.Components;

/// <summary>The device answer and the handoff that carries it across prerender→WebAssembly:
/// the side that can see the request resolves it and persists it, the side that cannot adopts
/// what was persisted. One source of truth, so a component's first client render answers the
/// same as its last server one.
///
/// On its own it answers no — a host with nothing to read the request with (the WebAssembly
/// client before a handoff, a component test) treats the machine as a desktop, which is the
/// answer that leaves every caller behaving as it does on one. The host that CAN see the
/// request supplies a derived provider (RequestDeviceProvider).</summary>
public class DeviceProvider : IDeviceProvider, IDisposable
{
    const string HandoffKey = "NSail.Device";

    readonly PersistentComponentState? _handoff;

    PersistingComponentStateSubscription _persisting;
    bool? _isPhone;

    public DeviceProvider(PersistentComponentState? handoff = null)
    {
        _handoff = handoff;
    }

    public bool IsPhone
    {
        get { return _isPhone ??= Decide(); }
    }

    /// <summary>What this host can tell about the machine from the request it is serving.</summary>
    protected virtual bool Resolve()
    {
        return false;
    }

    bool Decide()
    {
        if (_handoff is not null && _handoff.TryTakeFromJson<bool>(HandoffKey, out var handed))
        {
            return handed;
        }

        // Nothing was handed over, so this side is the one that resolves — and therefore the
        // one with an answer worth persisting. The render mode is named because the callback
        // belongs to a service rather than a component, and .NET refuses to guess for one; it
        // is WebAssembly because that is the only client that starts a second time in a second
        // process, with no request left to read.
        if (_handoff is not null)
        {
            _persisting = _handoff.RegisterOnPersisting(Persist, RenderMode.InteractiveWebAssembly);
        }

        return Resolve();
    }

    Task Persist()
    {
        _handoff?.PersistAsJson(HandoffKey, IsPhone);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _persisting.Dispose();

        GC.SuppressFinalize(this);
    }
}
