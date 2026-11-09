// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSail.Messaging;
using NSail.Messaging.Annotations;
using NSail.Messaging.Runtime;
using NSail.Messaging.WebApi;
using NSail.Security;

namespace NSail.BaseServices.WebApi.Tests;

/// <summary>The Sample's generated endpoints, probed anonymously with no body and no content type
/// against a host with no policy registered at all: every one must answer the gate's denial before
/// binding, so the sweep is total and keeps no anonymous-exception list.</summary>
public sealed class EndpointGateTests
{
    [Fact]
    public async Task Every_generated_endpoint_denies_an_anonymous_probe_before_binding()
    {
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseTestServer();
        builder.AddBaseWebApi();
        builder.Services.AddAuthentication();
        builder.Services.AddSecurityEnforcement();
        builder.Services.AcceptPermissiveProviders();

        var entries = typeof(NSail.Sample.Contacts.GetContact).Assembly.GetTypes()
            .Concat(System.Reflection.Assembly.Load("NSail.Sample.Web").GetTypes())
            .Where(t => !t.IsAbstract && typeof(IEndpointEntry).IsAssignableFrom(t))
            .ToList();

        foreach (var entry in entries)
        {
            builder.Services.AddTransient(typeof(IEndpointEntry), entry);
        }

        foreach (var message in typeof(NSail.Sample.Contacts.GetContact).Assembly.GetTypes()
            .Where(t => t.GetCustomAttributes(typeof(HttpAttribute), true).Length > 0))
        {
            builder.Services.AddSingleton(new MessageRegistration(message));
        }

        await using var app = builder.Build();

        app.UseBaseWebApi();

        await app.StartAsync();

        var probed = 0;
        var wrong = new List<string>();
        using var client = app.GetTestClient();

        foreach (var endpoint in app.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>())
        {
            if (endpoint.Metadata.GetMetadata<MessageEndpointMetadata>() is null)
            {
                continue;
            }

            var method = endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()!.HttpMethods[0];
            var path = Regex.Replace(endpoint.RoutePattern.RawText!, @"\{[^}]+\}", Guid.Empty.ToString());

            using var request = new HttpRequestMessage(new HttpMethod(method), new Uri($"https://localhost/{path}"));
            using var response = await client.SendAsync(request);
            probed++;

            if (response.StatusCode is not (HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized))
            {
                wrong.Add($"{method} {path} answered {(int)response.StatusCode}");
            }
        }

        Assert.True(probed > 0, "The sweep found no generated endpoint to probe.");
        Assert.True(wrong.Count == 0, "Every endpoint denies before binding: " + string.Join("; ", wrong));
    }
}
