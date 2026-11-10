// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSail.Harness.Board;
using NSail.Harness.Lanes;
using NSail.Harness.Repositories;

namespace NSail.Harness.GitHub;

public static class GitHubServices
{
    // GitHub as the provider named "GitHub": the board (Harness:Board), repositories and their story
    // locks (Harness:Repositories), and the lanes' credentials, with the token from the `GitHub` section.
    public static IServiceCollection AddGitHub(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(GitHubOptions.Section).Get<GitHubOptions>() ?? new GitHubOptions();
        var token = new GitHubToken(options.ResolveToken());
        // The harness's own git (clone, fetch, push) reaches GitHub through `gh auth git-credential`,
        // which reads GH_TOKEN: it pushes as the harness's token, never as whoever is logged in to gh
        // on the machine (and a machine with no login at all works the same). Only git gets it, not
        // the process environment every child would inherit.
        Lanes.Shell.GitEnvironment = new Dictionary<string, string> { ["GH_TOKEN"] = token.Value };
        services.AddSingleton(token);
        // A connection idle across the wait between passes is closed by the far end (in WSL, at the
        // NAT) while the pool still holds it, and the next pass's first call reads "response ended
        // prematurely": the pool lets go of idle connections before the shortest wait does.
        services.AddHttpClient<GitHubClient>(http => GitHubClient.Configure(http, token.Value))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { PooledConnectionIdleTimeout = TimeSpan.FromSeconds(15) });
        services.AddTransient<GitHubProviders>();
        services.AddTransient<IBoardProvider>(sp => sp.GetRequiredService<GitHubProviders>());
        services.AddTransient<IRepositoryProvider>(sp => sp.GetRequiredService<GitHubProviders>());
        services.AddSingleton<ILaneEnvironment>(sp => new GitHubLaneEnvironment(sp.GetRequiredService<HarnessOptions>(), token.Value));
        return services;
    }
}
