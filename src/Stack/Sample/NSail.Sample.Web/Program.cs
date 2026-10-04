// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.BaseServices.WebApi;
using NSail.BaseServices.WebApp;
using NSail.Data;
using NSail.Sample;

var builder = WebApplication.CreateBuilder(args);

builder.AddBaseWebApi();
builder.AddBaseWebApp();

builder.Services.AddSampleWebApi(builder.Configuration);
builder.Services.AddSampleWebApp();

var app = builder.Build();

await app.ApplyMigrations<SampleDbContext>();

app.UseBaseWebApi();
app.UseBaseWebApp();
app.MapReactClient();
app.MapRazorApp<App>();

app.Run();
