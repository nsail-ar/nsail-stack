// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Logging;
using NSail.Harness.Agents;
using NSail.Harness.GitHub;
using NSail.Harness.Lanes;
using NSail.Harness.Planning;
using NSail.Harness.Repositories;
using NSail.Harness.Workers;

namespace NSail.Harness.Tests;

// A whole builder job against a file:// remote: the seat commits in the slot and then dies or returns,
// and what reaches the remote, the board and the log is read back.
public class DevJobRunTests : IDisposable
{
    const string Story = "story/app-1";
    const string Rescue = "rescue/app-1";
    const string Workflow = ".github/workflows/ci.yml";

    readonly string _root = Path.Combine(Path.GetTempPath(), "harness-devjob-" + Guid.NewGuid().ToString("N"));

    string Remote => Path.Combine(_root, "remote.git");

    public void Dispose()
    {
        if (!Directory.Exists(_root)) return;
        foreach (var f in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
        Directory.Delete(_root, true);
    }

    // The repository as the job reaches it: pushes go to the slot's origin, and the story has no PR and no push yet.
    internal class FileRepository(string remote) : IRepository
    {
        readonly GitHubRepository _git = new(null!, new RepositoryOptions { Name = "acme/app" }, new StoryNames("app", ["story"]), "t");

        public string Name => "acme/app";
        public string DefaultBranch => "main";
        public string CloneUrl => remote;
        public string? CredentialHelper => null;

        public Task Push(string workDir, string branch, string? startedAt, CancellationToken ct = default)
        {
            return _git.Push(workDir, branch, startedAt, ct);
        }

        public List<PrRef> Open { get; } = [];

        public List<PrRef> Merged { get; } = [];

        public Task<IReadOnlyList<PrRef>> PrsOf(int story, string state, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<PrRef>>(state switch
            {
                "open" => [.. Open],
                "merged" => [.. Merged],
                _ => [],
            });
        }

        public Task<IReadOnlyList<DateTimeOffset>> PushTimes(int story, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<DateTimeOffset>>([]);
        }

        public Task<IReadOnlyList<OpenPr>> OpenPrs(CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public virtual Task<PullRequest> Pr(int number, CancellationToken ct = default)
        {
            var pr = Open.Concat(Merged).Single(p => p.Number == number);
            return Task.FromResult(new PullRequest(pr.Number, pr.Title, Open.Contains(pr), pr.Head, "", false));
        }

        public virtual Task<IReadOnlyList<PrNote>> Thread(int pr, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<PrNote>>([]);
        }

        public virtual Task<string> Login(CancellationToken ct = default)
        {
            return Task.FromResult("harness");
        }

        public Task<IReadOnlyList<string>> Files(int pr, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        public List<(int Pr, string Body)> Said { get; } = [];

        public Task Comment(int pr, string body)
        {
            Said.Add((pr, body));
            return Task.CompletedTask;
        }

        public List<(string Branch, string Title, string Body)> Opened { get; } = [];

        public Task<int> CreatePr(string branch, string title, string body, CancellationToken ct = default)
        {
            Opened.Add((branch, title, body));
            return Task.FromResult(100 + Opened.Count);
        }

        public Task ClosePr(int pr, string why, CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public virtual Task Merge(int pr, string sha, CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Merge? Landed { get; set; }

        public Task<Merge?> MergeOf(int story, CancellationToken ct = default)
        {
            return Task.FromResult(Landed);
        }

        public Task<CiState> Ci(string branch, string sha, CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public virtual Task<CiFailure?> Failure(string sha, CancellationToken ct = default)
        {
            return Task.FromResult<CiFailure?>(null);
        }

        public virtual Task<CiFailure?> MainFailure(CancellationToken ct = default)
        {
            return Task.FromResult<CiFailure?>(null);
        }

        public Task<string> Head(string branch, CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task SetStatus(string sha, string context, string state, string description, CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<StatusMark>> Statuses(int depth = 100, CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlySet<string>> RunningWorkflows(CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task<int> RunsSince(string workflow, DateTimeOffset at, CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task Dispatch(string workflow, object inputs, CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<Release>> Releases(CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public async Task<bool> Holds(string reference, string sha, CancellationToken ct = default)
        {
            return (await Shell.Run("git", ["merge-base", "--is-ancestor", sha, reference], remote)).ExitCode == 0;
        }

        public Task<string?> Variable(string name, CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }
    }

    // A seat that commits one file in its slot (none when path is null), leaves the PR's words when it
    // has any, then dies or returns.
    sealed class Seat(string? path, Exception? dies, string? words = null) : IAgent
    {
        public string Committed { get; private set; } = "";

        public int Runs { get; private set; }

        public string Driver => "test";

        public async Task<AgentResult> Run(AgentRun run, CancellationToken ct)
        {
            Runs++;
            if (words is not null) await File.WriteAllTextAsync(Path.Combine(run.WorkDir, ".voyage", "out", "pr.md"), words, ct);
            if (path is not null)
            {
                var file = Path.Combine(run.WorkDir, path);
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                await File.WriteAllTextAsync(file, "on: push\n", ct);
                await Shell.Git(run.WorkDir, "add", "-A", "--", path);
                await Shell.Git(run.WorkDir, "commit", "-q", "-m", "work");
                Committed = await Shell.Git(run.WorkDir, "rev-parse", "HEAD");
            }
            if (dies is not null) throw dies;
            return new AgentResult(0, false, null, false, null);
        }

        public IReadOnlyDictionary<string, string> ScriptEnvironment(string name)
        {
            return new Dictionary<string, string>();
        }

        public (bool Refused, DateTimeOffset? Until) Quota(string output)
        {
            return (false, null);
        }
    }

    internal sealed class Lines : ILoggerFactory, ILogger
    {
        public List<(LogLevel Level, string Text)> Written { get; } = [];

        public ILogger CreateLogger(string categoryName)
        {
            return this;
        }

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public void Dispose()
        {
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (Written) Written.Add((logLevel, formatter(state, exception)));
        }
    }

    async Task Seed()
    {
        Directory.CreateDirectory(_root);
        await Shell.Git(_root, "init", "-q", "--bare", "-b", "main", Remote);
        var seed = Path.Combine(_root, "seed");
        await Shell.Git(_root, "clone", "-q", Remote, seed);
        await Shell.Git(seed, "checkout", "-q", "-b", "main");
        await Shell.Git(seed, "-c", "user.name=t", "-c", "user.email=t@example.com", "commit", "-q", "--allow-empty", "-m", "M");
        await Shell.Git(seed, "push", "-q", "origin", "main");
    }

    // A pre-receive hook on the remote that refuses every ref matching the shell pattern.
    async Task Refuse(string pattern)
    {
        var hook = Path.Combine(Remote, "hooks", "pre-receive");
        await File.WriteAllTextAsync(hook, $"#!/bin/sh\nwhile read old new ref; do case \"$ref\" in {pattern}) exit 1;; esac; done\nexit 0\n");
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(hook, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    async Task<string?> RemoteHead(string branch)
    {
        var r = await Shell.Run("git", ["rev-parse", "-q", "--verify", $"refs/heads/{branch}"], Remote);
        return r.ExitCode == 0 ? r.Output.Trim() : null;
    }

    FileRepository? _repo;

    FileRepository Repo => _repo ??= new FileRepository(Remote);

    async Task<JobOutcome> Run(IAgent seat, MemoryBoard board, Lines logs)
    {
        var options = new HarnessOptions
        {
            WorkRoot = Path.Combine(_root, "work"),
            Repositories = [new RepositoryOptions { Name = "acme/app" }],
            ProtectedPaths = [".github/workflows/**"],
            Seats = { ["gru"] = new AgentOptions { Signature = "— Gru", Model = "m", Prompt = "build __ISSUE__" } },
            Workers = { ["develop"] = new WorkerOptions { Type = "Develop", Seat = "gru" } },
        };
        var repo = Repo;
        var workspace = new Workspace(Path.Combine(_root, "ws"), Remote, null, 1, "Gru", "gru@example.com");
        var ctx = new JobContext(board, new RepositorySet([(repo, options.Repositories[0])]), new Dictionary<string, Workspace> { [repo.Name] = workspace },
            seat, options, [], logs, new BuildLeases(1));
        var job = new DevJob(new WorkItem(WorkKind.Dev, 1, null, "rest", "rest", Worker: "develop"), ctx, new JobHistory(Path.Combine(_root, "history")));
        return await job.Run(1, Path.Combine(_root, "job.log"), CancellationToken.None);
    }

    static MemoryBoard Board()
    {
        var board = new MemoryBoard();
        board.Add(1, title: "a story", stage: Stages.Ready);
        return board;
    }

    // A seat that commits nothing and asks a question.
    sealed class Asking : IAgent
    {
        public string Driver => "test";

        public async Task<AgentResult> Run(AgentRun run, CancellationToken ct)
        {
            await File.WriteAllTextAsync(Path.Combine(run.WorkDir, ".voyage", "out", "ruling.md"), "Where does the fix land? 1. the Stack 2. here — I'd pick 1.", ct);
            return new AgentResult(0, false, null, false, null);
        }

        public IReadOnlyDictionary<string, string> ScriptEnvironment(string name)
        {
            return new Dictionary<string, string>();
        }

        public (bool Refused, DateTimeOffset? Until) Quota(string output)
        {
            return (false, null);
        }
    }

    [Fact]
    public async Task AReleaseRedsQuestionGoesToTheAnalystWithNoHold()
    {
        await Seed();
        var board = new MemoryBoard();
        board.Add(1, title: "Regression red on main (Acme)", stage: Stages.Ready);

        var outcome = await Run(new Asking(), board, new Lines());

        Assert.Equal("Reanalyze", outcome.Event);
        Assert.DoesNotContain(Labels.Ruling, board.Cards[1].Labels);
        Assert.Contains(board.Cards[1].Comments, c => c.Body.Contains("Where does the fix land?"));
        Assert.Contains(board.Cards[1].Comments, c => c.Body.Contains("waits for no hand"));
    }

    [Fact]
    public async Task AnOrdinaryStorysQuestionStillWaitsForAHand()
    {
        await Seed();
        var board = Board();

        var outcome = await Run(new Asking(), board, new Lines());

        Assert.Equal("Ruling", outcome.Event);
    }

    [Fact]
    public async Task ACancelledSeatsCommitsReachARescueRefAndTheCancellationPropagates()
    {
        await Seed();
        var seat = new Seat("work.txt", new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => Run(seat, Board(), new Lines()));

        Assert.Equal(seat.Committed, await RemoteHead(Rescue));
        Assert.Null(await RemoteHead(Story));
    }

    [Fact]
    public async Task ACrashedSeatsRescueTakesTheNextNameWhenTheRefExists()
    {
        await Seed();
        await Shell.Git(Remote, "update-ref", $"refs/heads/{Rescue}", "refs/heads/main");
        var taken = await RemoteHead(Rescue);
        var seat = new Seat("work.txt", new InvalidOperationException("crashed"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Run(seat, Board(), new Lines()));

        Assert.Equal(taken, await RemoteHead(Rescue));
        Assert.Equal(seat.Committed, await RemoteHead($"{Rescue}-2"));
    }

    [Fact]
    public async Task ARescueTheScreenHoldsPushesNothingAndSaysWhy()
    {
        await Seed();
        var logs = new Lines();

        await Assert.ThrowsAsync<OperationCanceledException>(() => Run(new Seat(Workflow, new OperationCanceledException()), Board(), logs));

        Assert.Null(await RemoteHead(Rescue));
        var line = Assert.Single(logs.Written, l => l.Level == LogLevel.Warning);
        Assert.Contains("protected path", line.Text);
        Assert.Contains(Labels.AllowProtected, line.Text);
    }

    [Fact]
    public async Task ACancelledSeatWithNothingAheadLogsNothing()
    {
        await Seed();
        var logs = new Lines();

        await Assert.ThrowsAsync<OperationCanceledException>(() => Run(new Seat(null, new OperationCanceledException()), Board(), logs));

        Assert.Null(await RemoteHead(Rescue));
        Assert.DoesNotContain(logs.Written, l => l.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task ARefusedPushRescuesNamesTheRefAndReleasesTheStory()
    {
        await Seed();
        await Refuse("refs/heads/story/*");
        var board = Board();
        var seat = new Seat("work.txt", null);

        var outcome = await Run(seat, board, new Lines());

        Assert.True(outcome.Failed);
        Assert.Equal(seat.Committed, await RemoteHead(Rescue));
        Assert.Null(await RemoteHead(Story));
        Assert.DoesNotContain(Labels.Claimed, board.Cards[1].Labels);
        Assert.Contains(board.Cards[1].Comments, c => c.Body.Contains($"`{Rescue}`", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ARefusedPushWhoseRescueFailsSaysTheCommitsStayLocal()
    {
        await Seed();
        await Refuse("*");
        var board = Board();

        var outcome = await Run(new Seat("work.txt", null), board, new Lines());

        Assert.True(outcome.Failed);
        Assert.Null(await RemoteHead(Rescue));
        Assert.DoesNotContain(Labels.Claimed, board.Cards[1].Labels);
        Assert.Contains(board.Cards[1].Comments, c => c.Body.Contains("local branch only", StringComparison.Ordinal));
    }

    // What a hand does on GitHub and the planner then does: `allow-protected` on, the screen's hold lifted.
    static void Allow(MemoryBoard board)
    {
        board.Cards[1].Labels.Add(Labels.AllowProtected);
        board.Cards[1].Labels.ExceptWith([Labels.Ruling, Labels.Screened]);
    }

    // A seat no voyage may call: the kept work goes up without one.
    internal sealed class NoSeat : IAgent
    {
        public string Driver => "test";

        public Task<AgentResult> Run(AgentRun run, CancellationToken ct)
        {
            throw new InvalidOperationException("no seat runs for kept work");
        }

        public IReadOnlyDictionary<string, string> ScriptEnvironment(string name)
        {
            return new Dictionary<string, string>();
        }

        public (bool Refused, DateTimeOffset? Until) Quota(string output)
        {
            return (false, null);
        }
    }

    [Fact]
    public async Task AProtectedRefusalHoldsTheStoryAsRuling()
    {
        await Seed();
        var board = Board();

        var outcome = await Run(new Seat(Workflow, null, "fix(stack): a workflow\n\nThe words."), board, new Lines());

        Assert.True(outcome.Failed);
        Assert.Null(await RemoteHead(Story));
        Assert.Superset(new HashSet<string> { Labels.Ruling, Labels.Screened }, board.Cards[1].Labels);
        Assert.DoesNotContain(Labels.Claimed, board.Cards[1].Labels);
        Assert.Equal(Stages.Ready, board.Cards[1].Stage);
        var said = Assert.Single(board.Cards[1].Comments);
        Assert.Contains($"`{Workflow}`", said.Body);
        Assert.Contains($"`{Labels.AllowProtected}`", said.Body);
        Assert.DoesNotContain(Markers.Voyage, said.Body);
        Assert.Empty(Repo.Opened);
    }

    [Fact]
    public async Task AProtectedRefusalWithAnOpenPrHoldsTheStoryWhereItStands()
    {
        await Seed();
        await Shell.Git(Remote, "update-ref", $"refs/heads/{Story}", "refs/heads/main");
        var head = await RemoteHead(Story);
        Repo.Open.Add(new PrRef(7, "a story", Story, null, "acme/app"));
        var board = Board();

        var outcome = await Run(new Seat(Workflow, null, "round words"), board, new Lines());

        Assert.True(outcome.Failed);
        Assert.Equal(head, await RemoteHead(Story));
        Assert.Superset(new HashSet<string> { Labels.Ruling, Labels.Screened }, board.Cards[1].Labels);
        Assert.Equal(Stages.InProgress, board.Cards[1].Stage);
        Assert.Contains(board.Cards[1].Comments, c => c.Body.Contains(Labels.AllowProtected, StringComparison.Ordinal));
        Assert.Empty(Repo.Said);
    }

    [Fact]
    public async Task ARefusalFollowedByADeadVoyageIsNotStalled()
    {
        await Seed();
        var board = Board();
        await Run(new Seat(Workflow, null), board, new Lines());
        board.Cards[1].Labels.ExceptWith([Labels.Ruling, Labels.Screened]);

        await Run(new Seat(null, null), board, new Lines());
        var outcome = await Run(new Seat(null, null), board, new Lines());

        Assert.DoesNotContain(Labels.Stalled, board.Cards[1].Labels);
        Assert.Equal("dead voyage (exit 0)", outcome.Summary);
    }

    [Fact]
    public async Task AReleasedStorysKeptWorkIsPushedWithoutASeat()
    {
        await Seed();
        var board = Board();
        var seat = new Seat(Workflow, null, "fix(stack): a workflow edit\n\nThe first builder's words.");
        await Run(seat, board, new Lines());
        Allow(board);

        var outcome = await Run(new NoSeat(), board, new Lines());

        Assert.Equal(seat.Committed, await RemoteHead(Story));
        var pr = Assert.Single(Repo.Opened);
        Assert.Equal(Story, pr.Branch);
        Assert.Contains("a workflow edit", pr.Title);
        Assert.Contains("The first builder's words.", pr.Body);
        Assert.Equal("Opened", outcome.Event);
    }

    [Fact]
    public async Task KeptWorkThatNoLongerAppliesIsRebuilt()
    {
        await Seed();
        var board = Board();
        await Run(new Seat(Workflow, null, "fix(stack): a workflow edit\n\nWords."), board, new Lines());
        Allow(board);
        await Shell.Git(Remote, "update-ref", $"refs/heads/{Story}", "refs/heads/main");
        var seat = new Seat("work.txt", null, "fix(stack): rebuilt\n\nNew words.");

        await Run(seat, board, new Lines());

        Assert.Equal(1, seat.Runs);
        Assert.Equal(seat.Committed, await RemoteHead(Story));
        Assert.Contains("New words.", Assert.Single(Repo.Opened).Body);
    }

    [Fact]
    public async Task KeptWorkForANewBranchIsRebuiltOnceTheStoryMergedSince()
    {
        await Seed();
        var board = Board();
        await Run(new Seat(Workflow, null, "fix(stack): a workflow edit\n\nStale words."), board, new Lines());
        Allow(board);
        Repo.Merged.Add(new PrRef(8, "a story", Story, DateTimeOffset.UtcNow, "acme/app"));
        var seat = new Seat("work.txt", null, "fix(stack): rebuilt\n\nNew words.");

        await Run(seat, board, new Lines());

        Assert.Equal(1, seat.Runs);
        Assert.Equal(seat.Committed, await RemoteHead(Story));
        Assert.Contains("New words.", Assert.Single(Repo.Opened).Body);
    }

    // CloseFixedReds on another machine while the seat builds: the story closes under the voyage.
    sealed class Closing(MemoryBoard board, IAgent inner, DateTimeOffset? wall = null) : IAgent
    {
        public string Driver => "test";

        public async Task<AgentResult> Run(AgentRun run, CancellationToken ct)
        {
            var result = await inner.Run(run, ct);
            await board.SetStage(1, Stages.Closed, ct);
            await board.Close(1, ct);
            return wall is null ? result : result with { QuotaRefused = true, QuotaUntil = wall };
        }

        public IReadOnlyDictionary<string, string> ScriptEnvironment(string name)
        {
            return new Dictionary<string, string>();
        }

        public (bool Refused, DateTimeOffset? Until) Quota(string output)
        {
            return (false, null);
        }
    }

    [Fact]
    public async Task AStoryClosedWhileTheSeatRanDeliversNothing()
    {
        await Seed();
        var board = Board();

        var outcome = await Run(new Closing(board, new Seat("work.txt", null, "fix(stack): a fix\n\nWords.")), board, new Lines());

        Assert.True(outcome.Idle);
        Assert.Null(await RemoteHead(Story));
        Assert.Null(await RemoteHead(Rescue));
        Assert.Empty(Repo.Opened);
        Assert.DoesNotContain(board.Cards[1].Comments, c => c.Body.Contains(Markers.Voyage, StringComparison.Ordinal));
        Assert.Equal(Stages.Closed, board.Cards[1].Stage);
        Assert.NotNull(board.Cards[1].ClosedAt);
        Assert.DoesNotContain(Labels.Claimed, board.Cards[1].Labels);
    }

    [Fact]
    public async Task AStoryClosedWhileTheSeatMetTheQuotaWallKeepsTheWall()
    {
        await Seed();
        var board = Board();
        var wall = DateTimeOffset.UtcNow.AddHours(3);

        var outcome = await Run(new Closing(board, new Seat("work.txt", null, "fix(stack): a fix\n\nWords."), wall), board, new Lines());

        Assert.True(outcome.Idle);
        Assert.Equal(wall, outcome.QuotaUntil);
        Assert.Empty(JobEvents.Fired(outcome));
        Assert.Empty(Repo.Opened);
    }
}
