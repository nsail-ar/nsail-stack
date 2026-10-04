// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using NSail.Messaging.Http;

namespace NSail.BaseServices.Wasm;

public class WasmUrlResolver : IUrlResolver
{
    readonly Uri baseAddress;

    public WasmUrlResolver(IWebAssemblyHostEnvironment environment)
    {
        baseAddress = new Uri(environment.BaseAddress, UriKind.Absolute);
    }

    public string Resolve(string urlTemplate)
    {
        // Uri.Port never reports the default as absent -- https gives 443 -- so the colon has to
        // go with the token, or a default-port address resolves to "https://host:" and fails to parse.
        var port = baseAddress.IsDefaultPort ? "" : $":{baseAddress.Port}";

        return urlTemplate
            .Replace($":{UrlTokens.Port}", port)
            .Replace(UrlTokens.Scheme, baseAddress.Scheme)
            .Replace(UrlTokens.Host, baseAddress.Host);
    }
}
