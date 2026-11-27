// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace NSail.Components;

/// <summary>The device answer and the handoff that carries it across prerender→WebAssembly:
/// the side that can see the request resolves it and persists it, the side that cannot adopts
/// what was persisted, so a component's first client render answers the same as its last
/// server one.
///
/// With nothing handed over, the WebAssembly client reads the same two facts off the browser
/// itself (ns.js, <c>nsapp.device</c>) and applies the same rule, so its answer never depends
/// on whether the prerender read the device or the handoff was still there. A host that can
/// read neither (a component test, a surface that is not a Blazor Web App) answers no, which
/// leaves every caller behaving as it does on a desktop. The host that serves the request
/// supplies a derived provider (RequestDeviceProvider).</summary>
public class DeviceProvider : IDeviceProvider, IDisposable
{
    const string HandoffKey = "NSail.Device";

    // The Client Hint made for exactly this bit, a structured boolean: "?1" is a phone and "?0"
    // anything else — including an iPad, which has reported a desktop user agent since iPadOS 13.
    const string Mobile = "?1";

    readonly PersistentComponentState? _handoff;
    readonly IJSRuntime? _browser;

    PersistingComponentStateSubscription _persisting;
    bool? _isPhone;

    public DeviceProvider(PersistentComponentState? handoff = null, IJSRuntime? browser = null)
    {
        _handoff = handoff;
        _browser = browser;
    }

    public bool IsPhone
    {
        get { return _isPhone ??= Decide(); }
    }

    /// <summary>What this host can tell about the machine by itself.</summary>
    protected virtual bool Resolve()
    {
        // Only WebAssembly answers interop synchronously; the prerender and a circuit do not,
        // and the request-side provider answers for them.
        if (_browser is not IJSInProcessRuntime browser)
        {
            return false;
        }

        try
        {
            var read = browser.Invoke<string?[]?>("nsapp.device");

            return read is [var hint, var agent] && IsPhoneBy(hint, agent);
        }
        catch (Exception failure) when (failure is JSException or InvalidOperationException or ObjectDisposedException)
        {
            return false;
        }
    }

    /// <summary>The one rule both sides answer by: the <c>Sec-CH-UA-Mobile</c> hint when the
    /// browser gives one, otherwise the user agent.</summary>
    protected static bool IsPhoneBy(string? mobileHint, string? userAgent)
    {
        if (!string.IsNullOrWhiteSpace(mobileHint))
        {
            return string.Equals(mobileHint.Trim(), Mobile, StringComparison.Ordinal);
        }

        return IsPhoneAgent(userAgent ?? string.Empty);
    }

    // The fallback, for the browsers that send no hint (Safari, Firefox). Android names the
    // form factor rather than the device: every Android browser puts "Mobile" in the token on
    // a phone and leaves it out on a tablet, which is the only thing that tells the two apart.
    static bool IsPhoneAgent(string agent)
    {
        if (agent.Contains("iPhone", StringComparison.OrdinalIgnoreCase)
            || agent.Contains("iPod", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return agent.Contains("Android", StringComparison.OrdinalIgnoreCase)
            && agent.Contains("Mobile", StringComparison.OrdinalIgnoreCase);
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
