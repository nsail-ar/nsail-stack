// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using NSail.Components;

namespace NSail.BaseServices.WebApp;

/// <summary>Reads the device class off the request the page is being rendered for, which is
/// what makes the answer available on the very first render — before any script runs and
/// before the WebAssembly client boots.</summary>
public sealed class RequestDeviceProvider : DeviceProvider
{
    // Chromium sends it on every navigation with no Accept-CH opt-in.
    const string MobileHint = "Sec-CH-UA-Mobile";

    readonly IHttpContextAccessor _requests;

    public RequestDeviceProvider(IHttpContextAccessor requests, PersistentComponentState? handoff = null)
        : base(handoff)
    {
        ArgumentNullException.ThrowIfNull(requests);

        _requests = requests;
    }

    protected override bool Resolve()
    {
        if (_requests.HttpContext?.Request.Headers is not { } headers)
        {
            return false;
        }

        return IsPhoneBy(headers[MobileHint].ToString(), headers.UserAgent.ToString());
    }
}
