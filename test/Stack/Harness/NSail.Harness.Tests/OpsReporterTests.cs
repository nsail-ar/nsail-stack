// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NSail.Harness.Api;
using NSail.Harness.Lanes;

namespace NSail.Harness.Tests;

// A report carries the ended jobs Ops has not accepted yet, as many as fit under the body's limit;
// only an accepted post settles them, on disk, so a refusal or a restart loses none.
public sealed class OpsReporterTests : IDisposable
{
    const string Token = "s3cret-ops-token";

    readonly string _root = Directory.CreateTempSubdirectory("ops-reporter").FullName;
    readonly DateTimeOffset _start = new(2026, 11, 9, 10, 0, 0, TimeSpan.Zero);

    public void Dispose()
    {
        Directory.Delete(_root, true);
    }

    sealed class Ops : HttpMessageHandler
    {
        public List<HttpStatusCode> Answers { get; } = [];
        public List<string> Bodies { get; } = [];
        public List<string?> Tokens { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            Tokens.Add(request.Headers.Authorization?.Parameter);
            var answer = Answers.Count >= Bodies.Count ? Answers[Bodies.Count - 1] : HttpStatusCode.OK;
            return new HttpResponseMessage(answer);
        }

        public IReadOnlyList<EndedJob> Jobs(int post)
        {
            return JsonSerializer.Deserialize<HarnessReport>(Bodies[post], JsonSerializerOptions.Web)!.Jobs!;
        }
    }

    sealed class Clients(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(handler, disposeHandler: false);
        }
    }

    sealed class Warnings : ILogger<OpsReporter>
    {
        public int Count { get; private set; }

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
            if (logLevel == LogLevel.Warning) Count++;
        }
    }

    JobHistory History()
    {
        return new JobHistory(_root);
    }

    static OpsReporter Reporter(JobHistory history, string root, Ops ops, Warnings? log = null)
    {
        var options = new HarnessOptions { Machine = "box", Ops = new OpsOptions { Url = "https://ops.example.com/", Token = Token } };
        return new OpsReporter(null!, options, new Clients(ops), new OpsLedger(root, history), log ?? new Warnings());
    }

    static Task<HarnessReport> Read(int running = 0)
    {
        var item = new WorkItem(WorkKind.Dev, 42, null, "p1", "work", Worker: "dev");
        var jobs = Enumerable.Range(1, running).Select(n => new RunningJob($"1109100000-dev-{n}", item, n, DateTimeOffset.UtcNow, $"/work/logs/{n}.log")
        {
            Doing = new string('x', JobActivity.LineCap),
            LastWrote = DateTimeOffset.UtcNow,
        }).ToList();
        return Task.FromResult(new HarnessReport { Machine = "box", Status = new HarnessStatus { Repository = "acme/app", Machine = "box", Running = jobs } });
    }

    void Ran(JobHistory history, int count, int from = 0)
    {
        for (var n = from; n < from + count; n++)
        {
            var started = _start.AddMinutes(n);
            history.Record(new JobRecord($"job-{n}", "Dev #42", 42, started, started.AddMinutes(38),
                "opened a PR for the story after a long round of building, testing and reading the thread", $"/work/logs/job-{n}.log", "failed"));
        }
    }

    [Fact]
    public async Task AReportCarriesTheEndedJobsWithTheirSevenFieldsAndNoLog()
    {
        var history = History();
        Ran(history, 1);
        history.Record(new JobRecord("job-running", "Dev #7", 7, _start, null, null, "/work/logs/job-running.log"));
        var ops = new Ops();

        await Reporter(history, _root, ops).Tick(_ => Read(), default);

        var job = Assert.Single(ops.Jobs(0));
        Assert.Equal(new EndedJob("job-0", "Dev #42", 42, _start, _start.AddMinutes(38), "opened a PR for the story after a long round of building, testing and reading the thread", "failed"), job);
        Assert.DoesNotContain("job-0.log", ops.Bodies[0]);
        Assert.DoesNotContain("logPath", ops.Bodies[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AJobWhoseOutcomeOutweighsTheLimitStillGoesWithItsOutcomeCut()
    {
        var history = History();
        Ran(history, 1);
        history.Record(new JobRecord("job-huge", "Dev #42", 42, _start, _start.AddMinutes(5), "failed: push refused: " + new string('é', 200_000), "/work/logs/job-huge.log", "broke"));
        Ran(history, 1, from: 1);
        var ops = new Ops();

        await Reporter(history, _root, ops).Tick(_ => Read(running: 16), default);

        Assert.Equal(["job-0", "job-huge", "job-1"], ops.Jobs(0).Select(j => j.Id));
        var huge = ops.Jobs(0)[1];
        Assert.Equal(OpsReporter.OutcomeChars, huge.Outcome!.Length);
        Assert.StartsWith("failed: push refused: ", huge.Outcome);
        Assert.True(Encoding.UTF8.GetByteCount(ops.Bodies[0]) <= new OpsOptions().MaxBytes);
    }

    [Fact]
    public async Task ALimitConfiguredTooLowStillCarriesJobs()
    {
        var history = History();
        Ran(history, 3);
        var ops = new Ops();
        var options = new HarnessOptions { Machine = "box", Ops = new OpsOptions { Url = "https://ops.example.com/", Token = Token, MaxBytes = 0 } };

        await new OpsReporter(null!, options, new Clients(ops), new OpsLedger(_root, history), new Warnings()).Tick(_ => Read(running: 16), default);

        Assert.Equal(["job-0", "job-1", "job-2"], ops.Jobs(0).Select(j => j.Id));
    }

    [Fact]
    public async Task AnAcceptedReportSettlesItsJobsAndTheNextOneCarriesOnlyNewOnes()
    {
        var history = History();
        Ran(history, 2);
        var ops = new Ops();
        var reporter = Reporter(history, _root, ops);

        await reporter.Tick(_ => Read(), default);
        Ran(history, 1, from: 2);
        await reporter.Tick(_ => Read(), default);
        await reporter.Tick(_ => Read(), default);

        Assert.Equal(["job-0", "job-1"], ops.Jobs(0).Select(j => j.Id));
        Assert.Equal(["job-2"], ops.Jobs(1).Select(j => j.Id));
        Assert.Empty(ops.Jobs(2));
    }

    [Fact]
    public async Task AfterRefusalsTheReportsCatchUpInPiecesUnderTheLimitUntilNothingIsOwed()
    {
        var history = History();
        Ran(history, 400);
        var ops = new Ops();
        ops.Answers.AddRange([HttpStatusCode.ServiceUnavailable, HttpStatusCode.Forbidden]);
        var reporter = Reporter(history, _root, ops);

        for (var tick = 0; tick < 12; tick++)
        {
            await reporter.Tick(_ => Read(), default);
        }

        Assert.All(ops.Bodies, b => Assert.True(Encoding.UTF8.GetByteCount(b) <= new OpsOptions().MaxBytes));
        var accepted = Enumerable.Range(2, ops.Bodies.Count - 2).SelectMany(ops.Jobs).Select(j => j.Id).ToList();
        Assert.Equal(Enumerable.Range(0, 400).Select(n => $"job-{n}"), accepted);
        Assert.True(ops.Jobs(2).Count < 400, "the first accepted report is one piece, not all of them");
        Assert.Empty(ops.Jobs(ops.Bodies.Count - 1));
    }

    [Fact]
    public async Task AJobTrimmedOffTheListIsNoLongerOwed()
    {
        var history = History();
        Ran(history, 10);
        var ops = new Ops();
        ops.Answers.Add(HttpStatusCode.ServiceUnavailable);
        var reporter = Reporter(history, _root, ops);

        await reporter.Tick(_ => Read(), default);
        var later = 10 + (int)JobHistory.Keep.TotalMinutes;
        Ran(history, 500, from: later);
        for (var tick = 0; tick < 6; tick++)
        {
            await reporter.Tick(_ => Read(), default);
        }

        var sent = Enumerable.Range(1, ops.Bodies.Count - 1).SelectMany(ops.Jobs).Select(j => j.Id).ToList();
        Assert.Equal(500, sent.Count);
        Assert.DoesNotContain("job-9", sent);
        Assert.Equal($"job-{later}", sent[0]);
    }

    [Fact]
    public async Task AHarnessNeverAcceptedOwesEveryEndedJobItsListHolds()
    {
        var history = History();
        Ran(history, 500);
        var ops = new Ops();
        var reporter = Reporter(history, _root, ops);

        for (var tick = 0; tick < 6; tick++)
        {
            await reporter.Tick(_ => Read(), default);
        }

        var sent = Enumerable.Range(0, ops.Bodies.Count).SelectMany(ops.Jobs).Select(j => j.Id).ToList();
        Assert.Equal(Enumerable.Range(0, 500).Select(n => $"job-{n}"), sent);
        Assert.True(ops.Jobs(0).Count < 500, "a full list goes across pieces");
    }

    [Fact]
    public async Task ARefusingOpsCostsOneWarningPerTickAndTheReporterKeepsTicking()
    {
        var history = History();
        Ran(history, 1);
        var ops = new Ops();
        ops.Answers.AddRange([HttpStatusCode.InternalServerError, HttpStatusCode.Unauthorized, HttpStatusCode.BadGateway]);
        var warnings = new Warnings();
        var reporter = Reporter(history, _root, ops, warnings);

        for (var tick = 0; tick < 3; tick++)
        {
            await reporter.Tick(_ => Read(), default);
        }
        await reporter.Tick(_ => throw new HttpRequestException("connection refused"), default);

        Assert.Equal(4, warnings.Count);
        Assert.Equal(3, ops.Bodies.Count);
        Assert.All(ops.Bodies, b => Assert.Single(JsonSerializer.Deserialize<HarnessReport>(b, JsonSerializerOptions.Web)!.Jobs!));
    }

    [Fact]
    public async Task EverySlotBusyAndAFullPieceOfJobsStayUnderTheCeilingWithNoToken()
    {
        var history = History();
        Ran(history, 500);
        var ops = new Ops();

        await Reporter(history, _root, ops).Tick(_ => Read(running: 16), default);

        Assert.True(Encoding.UTF8.GetByteCount(ops.Bodies[0]) <= new OpsOptions().MaxBytes);
        Assert.NotEmpty(ops.Jobs(0));
        Assert.Equal(16, JsonSerializer.Deserialize<HarnessReport>(ops.Bodies[0], JsonSerializerOptions.Web)!.Status.Running.Count);
        Assert.Equal(Token, ops.Tokens[0]);
        Assert.DoesNotContain(Token, ops.Bodies[0]);
    }

    [Fact]
    public async Task ARestartAfterAnUnacceptedPostSendsTheOwedJobs()
    {
        var history = History();
        Ran(history, 3);
        var ops = new Ops();
        ops.Answers.Add(HttpStatusCode.ServiceUnavailable);
        await Reporter(history, _root, ops).Tick(_ => Read(), default);
        await Reporter(history, _root, ops).Tick(_ => Read(), default);

        var again = new Ops();
        await Reporter(History(), _root, again).Tick(_ => Read(), default);

        Assert.Equal(["job-0", "job-1", "job-2"], ops.Jobs(1).Select(j => j.Id));
        Assert.Empty(again.Jobs(0));
    }
}
