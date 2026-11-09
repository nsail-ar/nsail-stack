// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.BaseServices.WebApi;
using NSail.BaseServices.WebApp;
using NSail.Configuration;
using NSail.Sample;

// Not for production: no sign-in is composed, so the API and its personal data answer anyone who
// can reach the port. Run it on localhost only, never on a network the internet can see.
var builder = WebApplication.CreateBuilder(args);

builder.AddBaseWebApi();
builder.AddBaseWebApp();

builder.Services.AddSampleWebApi(builder.Configuration);
builder.Services.AddSampleWebApp();

var app = builder.Build();

var sample = app.Configuration.Load<SampleOptions>();
await SampleRole.Prepare(sample.AdminConnectionString, sample.ConnectionString);

app.UseBaseWebApi();
app.UseBaseWebApp();
app.MapReactClient();
app.MapRazorApp<App>();

app.Run();
