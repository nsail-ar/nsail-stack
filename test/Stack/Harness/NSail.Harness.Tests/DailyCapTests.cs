// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSail.Harness.Agents;
using NSail.Harness.Jobs;
using NSail.Harness.Lanes;
using NSail.Harness.Planning;
using NSail.Harness.Watching;

namespace NSail.Harness.Tests;

// A worker's DailyCap: the story that already took that many jobs with an agent in a day takes
// `ruling` and one comment instead of the next, counted by kind of work since a hand last lifted a hold.
// In JobLimitTests' collection: that class counts the jobs every loop of the process ends.
[Collection(nameof(JobLimitTests))]
public sealed class DailyCapTests : IDisposable
{
    const int Story = 5;
    readonly string _root = Directory.CreateTempSubdirectory("daily-cap").FullName;
    readonly MemoryBoard _board = new();
    readonly JobHistory _history;
    readonly HarnessMetrics _metrics = new();
    readonly JobContext _jobs;
    readonly HarnessLoop _loop;
    int _n;

    public DailyCapTests()
    {
        _history = new JobHistory(Path.Combine(_root, "history"));
        var options = new HarnessOptions
        {
            Machine = "box",
            Repositories = [new RepositoryOptions { Name = "acme/app" }],
            Seats = { ["gru"] = new AgentOptions { Prompt = "build" } },
            Workers =
            {
                ["dev"] = new WorkerOptions { Type = "Develop", Seat = "gru", DailyCap = 3 },
                ["review"] = new WorkerOptions { Type = "Review", Seat = "gru" },
            },
        };
        var workspace = new Workspace(Path.Combine(_root, "ws"), "https://example.com/acme/app.git", null, 1, "Gru", "gru@example.com");
        var repo = new DevJobRunTests.FileRepository("unused");
        _jobs = new JobContext(_board, new RepositorySet([(repo, options.Repositories[0])]), new Dictionary<string, Workspace> { [repo.Name] = workspace }, new Answers(), options, [], NullLoggerFactory.Instance, new BuildLeases(1));
        _loop = new HarnessLoop(null!, _board, new JobLimitTests.Locks(), workspace,
            _history, new JobStats(null), _jobs, new BuildLeases(1), null!, Options.Create(options), null!, _metrics,
            new WatchState(null), new HarnessState(Path.Combine(_root, "state")), NullLogger<HarnessLoop>.Instance);
        _board.Add(Story);
    }

    public void Dispose()
    {
        _metrics.Dispose();
        Directory.Delete(_root, true);
    }

    static WorkItem Dev(string tier = "rest")
    {
        return new WorkItem(WorkKind.Dev, Story, null, tier, tier, Worker: tier == "manual" ? null : "dev");
    }

    void Ran(string holder, int count, TimeSpan ago, bool seated = true)
    {
        for (var i = 0; i < count; i++)
        {
            var started = DateTimeOffset.UtcNow - ago + TimeSpan.FromSeconds(_n++);
            _history.Record(new JobRecord($"{started:MMddHHmmss}{_n}-{holder}", holder, Story, started, started.AddMinutes(1), "handed off", "x.log", "ok", seated));
        }
    }

    [Fact]
    public async Task AStoryUnderItsWorkersCapStarts()
    {
        Ran("dev-5", 2, TimeSpan.FromHours(3));

        Assert.False(await _loop.Capped(Dev(), Story));
        Assert.DoesNotContain(Labels.Ruling, _board.Cards[Story].Labels);
        Assert.Empty(_board.Cards[Story].Comments);
    }

    [Fact]
    public async Task AStoryAtItsWorkersCapTakesRulingAndOneSignedComment()
    {
        Ran("dev-5", 3, TimeSpan.FromHours(3));

        Assert.True(await _loop.Capped(Dev(), Story));

        Assert.Contains(Labels.Ruling, _board.Cards[Story].Labels);
        var said = Assert.Single(_board.Cards[Story].Comments).Body;
        Assert.Contains("3 dev jobs", said, StringComparison.Ordinal);
        Assert.Contains("box", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnlyJobsThatRanAnAgentOverThatStoryInThatKindInTheDayCount()
    {
        Ran("dev-5", 2, TimeSpan.FromHours(3));
        Ran("dev-5", 4, TimeSpan.FromHours(3), seated: false);
        Ran("dev-15", 4, TimeSpan.FromHours(3));
        Ran("review-5", 4, TimeSpan.FromHours(3));
        Ran("dev-5", 4, TimeSpan.FromHours(30));

        Assert.False(await _loop.Capped(Dev(), Story));
    }

    [Fact]
    public async Task AHandsLiftStartsTheCountAgain()
    {
        Ran("dev-5", 3, TimeSpan.FromHours(3));
        _board.Lifts[Story] = DateTimeOffset.UtcNow.AddHours(-1);

        Assert.False(await _loop.Capped(Dev(), Story));

        Ran("dev-5", 3, TimeSpan.FromMinutes(30));

        Assert.True(await _loop.Capped(Dev(), Story));
    }

    [Fact]
    public async Task AJobSentByHandARedsStoryAndAWorkerWithNoCapAreNeverCapped()
    {
        Ran("dev-5", 5, TimeSpan.FromHours(3));
        Ran("review-5", 5, TimeSpan.FromHours(3));

        Assert.False(await _loop.Capped(Dev("manual"), Story));
        Assert.False(await _loop.Capped(new WorkItem(WorkKind.Review, Story, 9, "rest", "rest", Worker: "review"), Story));

        _board.Cards[Story].Title = new HarnessOptions().Texts.MainRedTitle;

        Assert.False(await _loop.Capped(Dev(), Story));
        Assert.DoesNotContain(Labels.Ruling, _board.Cards[Story].Labels);
    }

    [Fact]
    public async Task AJobIsRecordedSeatedOnlyWhenItRanAnAgent()
    {
        _board.Cards[Story].Stage = Stages.InProgress;

        var sat = await End(new Sits(Dev(), _jobs, seat: true));
        var passed = await End(new Sits(Dev(), _jobs, seat: false));

        Assert.True(sat.Seated);
        Assert.False(passed.Seated);
        Assert.Equal(1, _history.Seated("dev-5", DateTimeOffset.UtcNow.AddHours(-1)));
    }

    async Task<JobRecord> End(IJob job)
    {
        var id = $"{DateTimeOffset.UtcNow:MMddHHmmss}{_n++}-dev-5";
        await _loop.Sail(job, new RunningJob(id, job.Item, 1, DateTimeOffset.UtcNow, Path.Combine(_root, $"{id}.log")));
        return _history.Jobs().Last(j => j.Id == id && j.Ended is not null);
    }

    sealed class Sits(WorkItem item, JobContext jobs, bool seat) : IJob
    {
        public WorkItem Item => item;

        public async Task<JobOutcome> Run(int slot, string logPath, CancellationToken ct)
        {
            if (seat) await jobs.RunSeat(new AgentRun("go", ".", "model", AgentAccess.Builder, 1, TimeSpan.FromMinutes(1), new Dictionary<string, string>(), logPath), ct);
            return new JobOutcome("handed off");
        }
    }

    sealed class Answers : IAgent
    {
        public string Driver => "none";

        public Task<AgentResult> Run(AgentRun run, CancellationToken ct)
        {
            return Task.FromResult(new AgentResult(0, false, null, false, "done"));
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
}
