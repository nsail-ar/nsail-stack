// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using NSail.BaseServices.Wasm;
using NSail.Components;
using NSail.Messaging.Runtime;
using NSail.Sample;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.AddHttpClients();
builder.Services.AddComponentServices();
builder.Services.AddMessaging();

builder.Services.AddSampleWasm();

await builder.Build().RunAsync();
