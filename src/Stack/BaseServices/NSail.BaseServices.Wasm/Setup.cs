// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using NSail.Messaging.Http;

namespace NSail.BaseServices.Wasm;

public static class Setup
{
    public static void AddHttpClients(this WebAssemblyHostBuilder builder)
    {
        builder.Services.AddHttpClients(builder.Configuration, new WasmUrlResolver(builder.HostEnvironment));
    }
}