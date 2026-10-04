// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using NSail.Builds;
using System.Net.Http.Headers;

namespace NSail.Messaging.Http;

public static class Setup
{
    const string SectionName = "HttpClients";
    const string OriginTemplate = $"{UrlTokens.Scheme}://{UrlTokens.Host}:{UrlTokens.Port}";

    public static IServiceCollection AddHttpClients(this IServiceCollection services, IConfiguration configuration, IUrlResolver urlResolver)
    {
        // The factory is the transport itself, not a per-client detail: senders are generated
        // for every [Http] message whether or not a client is configured, so it has to exist
        // even when nothing under "HttpClients" ever names one.
        services.AddHttpClient();

        ApplyDefaultBaseAddress(services, urlResolver);
        WatchServerBuild(services);

        var section = configuration.GetSection(SectionName);

        if (!section.Exists())
        {
            return services;
        }

        var clients = section.Get<Dictionary<string, HttpClientOptions>>();

        if (clients == null || clients.Count == 0)
        {
            return services;
        }

        foreach (var (name, options) in clients)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl)
                ? null
                : urlResolver.Resolve(options.BaseUrl);

            services.AddHttpClient(name, client =>
            {
                if (baseUrl != null)
                {
                    client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
                }

                if (options.TimeoutSeconds.HasValue && options.TimeoutSeconds.Value > 0)
                {
                    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds.Value);
                }

                if (options.DefaultHeaders != null)
                {
                    foreach (var (headerName, headerValue) in options.DefaultHeaders)
                    {
                        if (string.IsNullOrWhiteSpace(headerName))
                        {
                            continue;
                        }

                        client.DefaultRequestHeaders.Remove(headerName);
                        client.DefaultRequestHeaders.TryAddWithoutValidation(headerName, headerValue);
                    }
                }

                client.DefaultRequestHeaders.AcceptCharset.Clear();
                client.DefaultRequestHeaders.AcceptCharset.Add(new StringWithQualityHeaderValue("utf-8"));
            });
        }

        return services;
    }

    // A kit served by the host that serves the app needs no configuration at all: its origin
    // is the host's own. Applying that default here means a kit left off "HttpClients"
    // still resolves instead of failing silently; explicit configuration is an override,
    // applied per name on top of this.
    static void ApplyDefaultBaseAddress(IServiceCollection services, IUrlResolver urlResolver)
    {
        var origin = urlResolver.Resolve(OriginTemplate);

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var baseAddress))
        {
            return;
        }

        // Runs for every named client, including names never registered under "HttpClients":
        // a per-name registration that sets its own base address still wins, whichever order
        // the two configurations run in.
        services.ConfigureAll<HttpClientFactoryOptions>(options =>
        {
            options.HttpClientActions.Add(client =>
            {
                client.BaseAddress ??= baseAddress;
            });
        });
    }

    // Every named client again, for the same reason the base address is: what the far side
    // answered with is a property of talking to it at all, not of any one client's
    // configuration. The singleton is what carries the answer out of the handler chain — the
    // factory builds a handler inside a scope of its own, so a scoped service here would be a
    // different instance from the one the screen is holding.
    static void WatchServerBuild(IServiceCollection services)
    {
        services.TryAddSingleton<ServerBuild>();
        services.TryAddTransient<ServerBuildHandler>();

        services.ConfigureAll<HttpClientFactoryOptions>(options =>
        {
            options.HttpMessageHandlerBuilderActions.Add(builder =>
            {
                builder.AdditionalHandlers.Add(builder.Services.GetRequiredService<ServerBuildHandler>());
            });
        });
    }
}
