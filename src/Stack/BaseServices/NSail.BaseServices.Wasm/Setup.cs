// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using NSail.Messaging.Http;
using NSail.Messaging.SignalR;

namespace NSail.BaseServices.Wasm;

public static class Setup
{
    public static void AddHttpClients(this WebAssemblyHostBuilder builder)
    {
        builder.Services.AddHttpClients(builder.Configuration, new WasmUrlResolver(builder.HostEnvironment));
    }

    /// <summary>The server's push, on the origin this client was served from — the same host
    /// every named client already defaults to.</summary>
    public static void AddPush(this WebAssemblyHostBuilder builder)
    {
        builder.Services.AddPush(new Uri(builder.HostEnvironment.BaseAddress));
    }
}