// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSail.Harness.Watching;

namespace NSail.Harness.OpenObserve;

public static class OpenObserveServices
{
    // OpenObserve as the log source named "OpenObserve", from the `OpenObserve` section. With no
    // Endpoint it adds nothing, and a Watch worker naming it fails at its first pass, saying so.
    public static IServiceCollection AddOpenObserve(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(OpenObserveOptions.Section).Get<OpenObserveOptions>() ?? new OpenObserveOptions();
        if (string.IsNullOrWhiteSpace(options.Endpoint)) return services;
        services.AddHttpClient<OpenObserveLogSource>(http => http.Timeout = TimeSpan.FromSeconds(60));
        services.AddSingleton(options);
        services.AddTransient<ILogSource>(sp => sp.GetRequiredService<OpenObserveLogSource>());
        return services;
    }
}
