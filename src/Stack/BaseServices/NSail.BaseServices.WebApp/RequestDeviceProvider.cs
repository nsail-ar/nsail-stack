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
    // The Client Hint made for exactly this bit. Chromium sends it on every navigation with no
    // Accept-CH opt-in, and it is a structured boolean: "?1" is a phone, "?0" is anything else
    // — including an iPad, which has reported a desktop user agent since iPadOS 13.
    const string MobileHint = "Sec-CH-UA-Mobile";
    const string Mobile = "?1";

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

        var hint = headers[MobileHint].ToString();

        if (!string.IsNullOrWhiteSpace(hint))
        {
            return string.Equals(hint.Trim(), Mobile, StringComparison.Ordinal);
        }

        return IsPhoneAgent(headers.UserAgent.ToString());
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
}
