// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Logging.Abstractions;
using NSail.Harness.Jobs;
using NSail.Harness.Lanes;
using NSail.Harness.Planning;
using NSail.Harness.Repositories;

namespace NSail.Harness.Tests;

// A cut that throws, for any reason, lets go of its claim on the head before the error leaves the job.
public sealed class ReleaseClaimTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "harness-release-" + Guid.NewGuid().ToString("N"));

    string Remote => Path.Combine(_root, "remote.git");

    public void Dispose()
    {
        if (!Directory.Exists(_root)) return;
        foreach (var f in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
        Directory.Delete(_root, true);
    }

    // The cut's first read throws; every status written is kept.
    sealed class Throwing(string remote, Exception e) : DevJobRunTests.FileRepository(remote), IRepository
    {
        public List<(string Context, string State)> Written { get; } = [];

        public new Task SetStatus(string sha, string context, string state, string description, CancellationToken ct = default)
        {
            Written.Add((context, state));
            return Task.CompletedTask;
        }

        public new Task<IReadOnlyList<StatusMark>> Statuses(int depth = 100, CancellationToken ct = default)
        {
            throw e;
        }
    }

    public static TheoryData<Exception> Endings => [
        new ProviderTimeoutException("timed out GET /repos/acme/app/statuses/abc: canceled", new TaskCanceledException()),
        new OperationCanceledException(),
        new InvalidOperationException("boom"),
    ];

    [Theory]
    [MemberData(nameof(Endings))]
    public async Task ACutThatThrowsLetsGoOfItsClaim(Exception e)
    {
        Directory.CreateDirectory(_root);
        await Shell.Git(_root, "init", "-q", "--bare", "-b", "main", Remote);
        var seed = Path.Combine(_root, "seed");
        await Shell.Git(_root, "clone", "-q", Remote, seed);
        await Shell.Git(seed, "-c", "user.name=t", "-c", "user.email=t@example.com", "commit", "-q", "--allow-empty", "-m", "M");
        await Shell.Git(seed, "push", "-q", "origin", "HEAD:main");
        var options = new HarnessOptions
        {
            Machine = "box",
            WorkRoot = Path.Combine(_root, "work"),
            Repositories = [new RepositoryOptions { Name = "acme/app" }],
            Workers = { ["release"] = new WorkerOptions { Type = "Release", Product = "Shop" } },
        };
        var repo = new Throwing(Remote, e);
        var workspace = new Workspace(Path.Combine(_root, "ws"), Remote, null, 1, "Gru", "gru@example.com");
        var ctx = new JobContext(new MemoryBoard(), new RepositorySet([(repo, options.Repositories[0])]), new Dictionary<string, Workspace> { [repo.Name] = workspace },
            new NoAgent(), options, [], NullLoggerFactory.Instance, new BuildLeases(1));
        var job = new ReleaseJob(new WorkItem(WorkKind.Release, null, null, "release", "the day's cut", "release-shop.yml", Worker: "release", ProductName: "Shop"), ctx);

        var thrown = await Assert.ThrowsAnyAsync<Exception>(() => job.Run(1, Path.Combine(_root, "job.log"), CancellationToken.None));

        Assert.Same(e, thrown);
        Assert.Equal([(Markers.HeldContext("shop"), "failure"), (Markers.HeldContext("shop"), "success")], repo.Written);
    }
}
