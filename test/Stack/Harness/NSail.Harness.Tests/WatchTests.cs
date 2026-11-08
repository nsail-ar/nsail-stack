// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NSail.Harness.GitHub;
using NSail.Harness.Jobs;
using NSail.Harness.OpenObserve;
using NSail.Harness.Watching;

namespace NSail.Harness.Tests;

// The telemetry watch: error lines grouped by fingerprint, a story filed per new group, counted on
// the open one, filed again when its story closed and it came back, muted when it closed as not planned.
public class WatchTests
{
    sealed class Source(params LoggedError[] lines) : ILogSource
    {
        public string Name => "logs";

        public Task<IReadOnlyList<LoggedError>> Errors(DateTimeOffset since, DateTimeOffset until, int max, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<LoggedError>>(lines.Where(l => l.At > since && l.At <= until).ToList());
        }
    }

    static LoggedError Line(string template = "Background job {Job} failed", string cell = "optical-test", string service = "Optical", int minutesAgo = 10, string? exception = "System.InvalidOperationException")
    {
        return new LoggedError(DateTimeOffset.UtcNow.AddMinutes(-minutesAgo), service, "NSail.Background.BackgroundJobRunner", template, exception,
            "   at Somewhere.Run()", cell, "trace1", new Uri("https://logs.example/web/logs"));
    }

    static async Task<string> Pass(MemoryBoard board, ILogSource source, WatchState? state = null, int? max = null)
    {
        var o = new HarnessOptions
        {
            Repositories = [new RepositoryOptions { Name = "acme/app" }],
            Products = { ["optical"] = new ProductOptions { Name = "Optical", Labels = ["area:optical"] } },
            Workers = { ["watch"] = new WorkerOptions { Type = "Watch", Source = "logs", MaxStories = max, Areas = { ["harness"] = "area:stack" } } },
        };
        var names = o.Names();
        var repo = new GitHubRepository(null!, o.Repositories[0], names, "t");
        var ctx = new JobContext(board, new RepositorySet([(repo, o.Repositories[0])]), new Dictionary<string, Lanes.Workspace>(), new NoAgent(), o, [],
            NullLoggerFactory.Instance, new Planning.BuildLeases(1)) { LogSources = [source] };
        var job = new WatchJob(new WorkItem(WorkKind.Watch, null, null, "watch", "due", Worker: "watch"), ctx, state ?? new WatchState(null));
        var log = Path.GetTempFileName();
        try
        {
            return (await job.Run(0, log, CancellationToken.None)).Summary;
        }
        finally
        {
            File.Delete(log);
        }
    }

    [Fact]
    public void LinesOfOneBugGroupAcrossCellsAndSplitByException()
    {
        var groups = ErrorGroup.Of([Line(cell: "optical-test"), Line(cell: "optical-canary"), Line(exception: "System.TimeoutException")]);

        Assert.Equal(2, groups.Count);
        Assert.Equal(2, groups[0].Count);
        Assert.Equal(["optical-canary", "optical-test"], groups[0].Cells);
    }

    [Theory]
    [InlineData("optical-test", false)]
    [InlineData("therapy-qa", false)]
    [InlineData("optical-canary", false)]
    [InlineData("optical-lumina", true)]
    [InlineData("optical-prod", true)]
    [InlineData("dev1", false)]
    public void ACellIsProductionUnlessItsSuffixSaysOtherwise(string cell, bool prod)
    {
        Assert.Equal(prod, ErrorGroup.IsProd(cell, ["test", "qa", "canary"]));
    }

    [Fact]
    public async Task ANewErrorIsFiledInAnalyzeWithItsAreaAndFingerprint()
    {
        var board = new MemoryBoard();

        await Pass(board, new Source(Line(), Line()));

        var card = Assert.Single(board.Cards).Value;
        Assert.Equal("Analyze", card.Stage);
        Assert.Equal(["area:optical", "telemetry"], card.Labels.Order());
        Assert.StartsWith("telemetry(optical): Background job {Job} failed", card.Title);
        Assert.EndsWith("(InvalidOperationException)", card.Title);
        Assert.Contains("AC1", card.Body);
        Assert.Contains("2 times", card.Body);
        Assert.NotNull(WatchStory.FingerprintIn(card.Body));
    }

    [Fact]
    public async Task AnErrorSeenInProductionWearsBug()
    {
        var board = new MemoryBoard();

        await Pass(board, new Source(Line(cell: "optical-lumina")));

        Assert.Contains("bug", Assert.Single(board.Cards).Value.Labels);
    }

    [Fact]
    public async Task AServiceNoProductNamesTakesTheWorkersArea()
    {
        var board = new MemoryBoard();

        await Pass(board, new Source(Line(service: "harness", cell: "dev1")));

        var card = Assert.Single(board.Cards).Value;
        Assert.Contains("area:stack", card.Labels);
        Assert.DoesNotContain("bug", card.Labels);
    }

    [Fact]
    public async Task AnOpenStoryIsCountedOnceADayNotFiledAgain()
    {
        var board = new MemoryBoard();
        await Pass(board, new Source(Line(minutesAgo: 30)));
        var n = Assert.Single(board.Cards).Key;

        await Pass(board, new Source(Line(minutesAgo: 20)));
        await Pass(board, new Source(Line(minutesAgo: 5)));

        Assert.Single(board.Cards);
        Assert.Single(board.Cards[n].Comments);
        Assert.Contains("Still logged", board.Cards[n].Comments[0].Body);
    }

    [Fact]
    public async Task AnErrorBackAfterItsStoryClosedIsFiledAgainNamingTheOldOne()
    {
        var board = new MemoryBoard();
        var line = Line(minutesAgo: 5);
        var fp = ErrorGroup.FingerprintOf(line.Service, line.Source, line.Template, line.ExceptionType);
        board.Add(7, "old", $"…\n{WatchStory.Marker(fp)}", "Closed", DateTimeOffset.UtcNow.AddHours(-1), false, "telemetry");

        await Pass(board, new Source(line));

        var filed = board.Cards.Single(c => c.Key != 7).Value;
        Assert.Contains("It broke again", filed.Body);
        Assert.Contains("#7", filed.Body);
    }

    [Fact]
    public async Task LinesFromBeforeTheStoryClosedFileNothing()
    {
        var board = new MemoryBoard();
        var line = Line(minutesAgo: 90);
        var fp = ErrorGroup.FingerprintOf(line.Service, line.Source, line.Template, line.ExceptionType);
        board.Add(7, "old", WatchStory.Marker(fp), "Closed", DateTimeOffset.UtcNow.AddHours(-1), false, "telemetry");

        await Pass(board, new Source(line));

        Assert.Single(board.Cards);
    }

    [Fact]
    public async Task AStoryClosedAsNotPlannedMutesItsError()
    {
        var board = new MemoryBoard();
        var line = Line(minutesAgo: 5);
        var fp = ErrorGroup.FingerprintOf(line.Service, line.Source, line.Template, line.ExceptionType);
        board.Add(7, "noise", WatchStory.Marker(fp), "Closed", DateTimeOffset.UtcNow.AddHours(-1), true, "telemetry");

        await Pass(board, new Source(line));

        Assert.Single(board.Cards);
    }

    [Fact]
    public async Task APassFilesAtMostItsMaxAndReadsTheRestAgainNextTime()
    {
        var board = new MemoryBoard();
        var state = new WatchState(null);
        var source = new Source(Line("a", minutesAgo: 30), Line("b", minutesAgo: 20), Line("c", minutesAgo: 10));

        var first = await Pass(board, source, state, max: 2);
        await Pass(board, source, state, max: 2);

        Assert.Contains("1 left for the next pass", first);
        Assert.Equal(3, board.Cards.Count);
    }

    [Fact]
    public async Task APassWithNoErrorsFilesNothing()
    {
        var board = new MemoryBoard();

        Assert.Equal("no errors logged", await Pass(board, new Source()));
        Assert.Empty(board.Cards);
    }

    [Fact]
    public void OpenObservesHitsBecomeErrorLines()
    {
        var answer = JsonDocument.Parse("""
            {"hits":[{"_timestamp":1793942052263069,"body":"{Item} failed","service_name":"harness","instrumentation_library_name":"NSail.Harness.HarnessLoop",
              "exception_type":"NSail.Harness.GitHub.GitHubException","exception_stacktrace":"   at X","nsail_tenant":"dev1","severity":"Error"},
              {"body":"no stamp"}]}
            """).RootElement;

        var line = Assert.Single(OpenObserveLogSource.Rows(answer, "https://logs.example", new OpenObserveOptions()));

        Assert.Equal("harness", line.Service);
        Assert.Equal("{Item} failed", line.Template);
        Assert.Equal("dev1", line.Cell);
        Assert.Equal("NSail.Harness.GitHub.GitHubException", line.ExceptionType);
        Assert.Null(line.TraceId);
        Assert.Equal(2026, line.At.Year);
        Assert.StartsWith("https://logs.example/web/logs?org_identifier=default", line.Link!.ToString());
        Assert.Null(line.ProviderStatus);
    }

    // A failed pass's record carries what the provider answered; the story filed for it names it,
    // so a revoked token reads apart from a 5xx and a rate limit.
    [Fact]
    public void AFailedPassesStoryNamesTheStatusAndTheCall()
    {
        var answer = JsonDocument.Parse($$"""
            {"hits":[{"_timestamp":1793942052263069,"body":"{{HarnessLoop.FailedAnswered}}",
              "service_name":"harness","instrumentation_library_name":"NSail.Harness.HarnessLoop","exception_type":"NSail.Harness.GitHub.GitHubException",
              "exception_stacktrace":"   at X","nsail_tenant":"dev1","severity":"Error","providercall":"Statuses","ProviderStatus":401,"ratelimitremaining":"4999"}]}
            """).RootElement;

        var line = Assert.Single(OpenObserveLogSource.Rows(answer, "https://logs.example", new OpenObserveOptions()));
        var body = WatchStory.Body(Assert.Single(ErrorGroup.Of([line])), prod: false, again: null);

        Assert.Equal("Statuses", line.ProviderCall);
        Assert.Equal(401, line.ProviderStatus);
        Assert.Equal(4999L, line.RateLimitRemaining);
        Assert.Contains("the provider answered `401` to `Statuses`, with `4999` calls left on its rate limit", body);
        var title = WatchStory.Title(Assert.Single(ErrorGroup.Of([line])), "stack");
        Assert.StartsWith("telemetry(stack): pass failed — nothing started", title);
        Assert.EndsWith("… (GitHubException)", title);
    }
}
