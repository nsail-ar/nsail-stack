// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSail.Harness.Agents;

namespace NSail.Harness.Claude;

public static class ClaudeServices
{
    // Claude Code as the seats' agent, configured from the `Claude` section.
    public static IServiceCollection AddClaude(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ClaudeOptions>(configuration.GetSection(ClaudeOptions.Section));
        services.AddSingleton<IAgent>(sp => new ClaudeAgent(
            sp.GetRequiredService<IOptions<ClaudeOptions>>().Value,
            Path.Combine(sp.GetRequiredService<HarnessOptions>().WorkRoot, "claude")));
        return services;
    }
}
