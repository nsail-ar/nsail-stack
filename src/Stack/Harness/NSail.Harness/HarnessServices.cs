// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Options;
using NSail.Harness.Agents;
using NSail.Harness.Api;
using NSail.Harness.Board;
using NSail.Harness.Jobs;
using NSail.Harness.Lanes;
using NSail.Harness.Locks;
using NSail.Harness.Planning;
using NSail.Harness.Repositories;
using NSail.Messaging.Runtime;
using NSail.Messaging.WebApi;

namespace NSail.Harness;

public static class HarnessServices
{
    // The harness itself — the loop, the planner's reader, the slots, the state, the build turns —
    // configured from the `Harness` section. The boards, the code hosts and the agent come from
    // their own providers (AddGitHub, AddClaude, ...), named by the configuration.
    public static IServiceCollection AddHarness(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(HarnessOptions.Section);
        // The old list would bind to nothing and start on an empty board.
        if (section.GetSection("Boards").Exists()) throw new InvalidOperationException("Harness:Boards is gone: one harness runs one board, under Harness:Board");
        services.Configure<HarnessOptions>(section);
        services.AddSingleton(sp =>
        {
            var o = sp.GetRequiredService<IOptions<HarnessOptions>>().Value;
            o.Validate();
            return o;
        });
        services.AddTransient(sp =>
        {
            var o = sp.GetRequiredService<HarnessOptions>();
            var names = o.Names();
            return new RepositorySet(o.Repositories.Select(r => (RepositoryProvider(sp, r).Open(r, names), r)).ToList());
        });
        services.AddTransient(sp =>
        {
            var o = sp.GetRequiredService<HarnessOptions>();
            var provider = sp.GetServices<IBoardProvider>().FirstOrDefault(p => p.Name == o.Board.Provider)
                ?? throw new InvalidOperationException($"no board provider named {o.Board.Provider}: add it to the host (AddGitHub, ...)");
            var board = provider.Open(section.GetSection(nameof(HarnessOptions.Board)));
            return o.Board.Stages.Count + o.Board.Labels.Count == 0 ? board : new MappedBoard(board, o.Board.Stages, o.Board.Labels);
        });
        services.AddTransient(sp =>
        {
            var o = sp.GetRequiredService<HarnessOptions>();
            return RepositoryProvider(sp, o.DefaultRepository).Locks(o.DefaultRepository, o.Key);
        });
        services.AddTransient<SnapshotReader>();
        services.AddSingleton(sp => new JobHistory(sp.GetRequiredService<HarnessOptions>().WorkRoot));
        services.AddSingleton(sp => new JobStats(sp.GetRequiredService<HarnessOptions>().WorkRoot));
        services.AddSingleton(sp => new BuildLeases(sp.GetRequiredService<HarnessOptions>().Builds));
        services.AddSingleton(sp =>
        {
            var o = sp.GetRequiredService<HarnessOptions>();
            var repos = sp.GetRequiredService<RepositorySet>();
            // The default repository's slots keep their old name: what a build left there stays warm.
            var workspaces = repos.All.ToDictionary(r => r.Name, r => Slots(
                Path.Combine(o.WorkRoot, repos.IsDefault(r.Name) ? "slots" : $"slots-{r.Name.Split('/').Last()}"), r, o));
            return new JobContext(
                sp.GetRequiredService<IBoard>(),
                repos,
                workspaces,
                sp.GetRequiredService<IAgent>(),
                o,
                sp.GetServices<ILaneEnvironment>(),
                sp.GetRequiredService<ILoggerFactory>(),
                sp.GetRequiredService<BuildLeases>())
            {
                LogSources = sp.GetServices<Watching.ILogSource>().ToList(),
            };
        });
        services.AddSingleton(sp => new Watching.WatchState(sp.GetRequiredService<HarnessOptions>().WorkRoot));
        services.AddSingleton(sp => sp.GetRequiredService<JobContext>().Workspace);
        services.AddSingleton<HarnessMetrics>();
        // The load a worker may ask about over the longest window it may ask for.
        services.AddSingleton(sp => new LoadSamples(TimeSpan.FromMinutes(Math.Max(15, LoadWindows(sp.GetRequiredService<HarnessOptions>()).DefaultIfEmpty(0).Max()))));
        services.AddHostedService<MachineLoad>();
        services.AddSingleton<HarnessLoop>();
        services.AddHostedService(sp => sp.GetRequiredService<HarnessLoop>());
        services.AddHttpClient(nameof(OpsReporter));
        services.AddHostedService<OpsReporter>();

        // The API (NSail.Harness.Sdk's messages) over the Stack's messaging.
        services.AddMessaging();
        services.AddMessagingJson();
        services.AddErrorHandler();
        services.AddHarnessHandlers();
        services.AddHarnessEndpoints();
        return services;
    }

    public static void MapHarnessApi(this WebApplication app)
    {
        app.UseErrorHandler();
        app.UseMiddleware<ControlGate>();
        app.MapEndpoints();
    }

    static IEnumerable<int> LoadWindows(HarnessOptions o)
    {
        return o.Workers.Values.Select(w => w.When).Concat(o.Conditions.Values).SelectMany(c => new[] { c.Cpu?.Minutes ?? 0, c.Memory?.Minutes ?? 0 });
    }

    static IRepositoryProvider RepositoryProvider(IServiceProvider sp, RepositoryOptions r)
    {
        return sp.GetServices<IRepositoryProvider>().FirstOrDefault(p => p.Name == r.Provider)
            ?? throw new InvalidOperationException($"no code host provider named {r.Provider} for {r.Name}: add it to the host (AddGitHub, ...)");
    }

    static Workspace Slots(string root, IRepository repo, HarnessOptions o)
    {
        return new Workspace(root, repo.CloneUrl, repo.CredentialHelper, o.MostLanes, o.CommitName, o.CommitEmail, repo.DefaultBranch);
    }
}
