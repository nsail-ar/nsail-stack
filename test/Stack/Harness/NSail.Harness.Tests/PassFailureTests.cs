// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Diagnostics.Metrics;
using System.Net;
using Microsoft.Extensions.Logging;
using NSail.Harness.GitHub;
using NSail.Telemetry;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace NSail.Harness.Tests;

// A pass whose read fails starts nothing, and its record carries what the provider answered in the
// only kinds the log scrubber lets reach the store: the status and the rate limit left as numbers,
// the call as an enum. The provider's body stays in the exception's message, which never travels.
public class PassFailureTests
{
    const string Template = HarnessLoop.FailedAnswered;

    sealed class Answer(HttpStatusCode status, string body, string? remaining) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(status) { RequestMessage = request, Content = new StringContent(body) };
            if (remaining is not null) response.Headers.Add("x-ratelimit-remaining", remaining);
            return Task.FromResult(response);
        }
    }

    static async Task<Exception> StatusesFailing(HttpStatusCode status, string body, string? remaining = null)
    {
        var http = new HttpClient(new Answer(status, body, remaining));
        GitHubClient.Configure(http, "t");
        var o = new HarnessOptions { Repositories = [new RepositoryOptions { Name = "acme/app" }] };
        var repo = new GitHubRepository(new GitHubClient(http), o.Repositories[0], o.Names(), "t");
        return await Assert.ThrowsAsync<GitHubException>(() => repo.Statuses());
    }

    [Fact]
    public async Task A502TravelsAsItsStatusAndCallAndNeverAsGitHubsBody()
    {
        var e = await StatusesFailing(HttpStatusCode.BadGateway, "Something went wrong while executing your query", "4980");

        var (pass, record) = Failed(e, PassCall.Statuses);

        Assert.Equal(Template, record.Body);
        Assert.Equal(LogLevel.Error, record.Level);
        Assert.Equal(502, record.Tag("ProviderStatus"));
        Assert.Equal(PassCall.Statuses, record.Tag("ProviderCall"));
        Assert.Equal(4980L, record.Tag("RateLimitRemaining"));
        Assert.Equal(typeof(GitHubException).FullName, record.Tag("exception.type"));
        Assert.DoesNotContain("Something went wrong", record.Flattened, StringComparison.Ordinal);
        Assert.DoesNotContain("POST /graphql", record.Flattened, StringComparison.Ordinal);
        Assert.All(record.Attributes.Where(a => a.Key is not "{OriginalFormat}" and not "exception.type" and not "exception.stacktrace"),
            a => Assert.IsNotType<string>(a.Value));
        Assert.Null(pass.Plan);
        Assert.Empty(pass.Started);
        Assert.Contains("Something went wrong", pass.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A403OnARateLimitReadsApartByTheCallsLeft()
    {
        var e = await StatusesFailing(HttpStatusCode.Forbidden, "API rate limit exceeded", "0");

        var (_, record) = Failed(e, PassCall.Stories);

        Assert.Equal(403, record.Tag("ProviderStatus"));
        Assert.Equal(0L, record.Tag("RateLimitRemaining"));
        Assert.Equal(PassCall.Stories, record.Tag("ProviderCall"));
    }

    [Fact]
    public async Task AFailedPassCountsItsStatus()
    {
        var e = await StatusesFailing(HttpStatusCode.BadGateway, "bad gateway");
        using var passes = new Passes();

        Failed(e, PassCall.Statuses, passes.Metrics);

        var counted = Assert.Single(passes.Seen);
        Assert.Equal("failed", counted.Outcome);
        Assert.Equal(502, counted.Status);
    }

    [Fact]
    public void ATimeoutLogsAndCountsAsBeforeWithNoStatus()
    {
        using var passes = new Passes();

        var (pass, record) = Failed(new TaskCanceledException("timed out"), PassCall.Statuses, passes.Metrics);

        Assert.Equal("pass failed — nothing started", record.Body);
        Assert.Equal(LogLevel.Error, record.Level);
        Assert.DoesNotContain(record.Attributes, a => a.Key is "ProviderStatus" or "ProviderCall" or "RateLimitRemaining");
        Assert.Empty(pass.Started);
        var counted = Assert.Single(passes.Seen);
        Assert.Equal("failed", counted.Outcome);
        Assert.False(counted.Tagged);
    }

    [Fact]
    public void AGoodPassCountsOkWithNoStatus()
    {
        using var passes = new Passes();

        passes.Metrics.Passed(failed: false);

        var counted = Assert.Single(passes.Seen);
        Assert.Equal("ok", counted.Outcome);
        Assert.False(counted.Tagged);
    }

    static (Pass Pass, Seen Record) Failed(Exception e, PassCall call, HarnessMetrics? metrics = null)
    {
        var records = new Recorder();
        using var factory = LoggerFactory.Create(logging =>
        {
            logging.AddOpenTelemetry(telemetry =>
            {
                telemetry.AddProcessor(new LogScrubber());
                telemetry.AddProcessor(records);
            });
        });
        using var own = metrics is null ? new HarnessMetrics() : null;
        var pass = HarnessLoop.Failed(factory.CreateLogger<HarnessLoop>(), metrics ?? own!, e, call, DateTimeOffset.UtcNow);
        return (pass, Assert.Single(records.Seen));
    }

    // LogRecord instances are pooled: what a test keeps is what it read on the way out.
    sealed record Seen(string? Body, LogLevel Level, IReadOnlyList<KeyValuePair<string, object?>> Attributes, string Flattened)
    {
        public object? Tag(string key)
        {
            return Attributes.FirstOrDefault(a => a.Key == key).Value;
        }
    }

    sealed class Recorder : BaseProcessor<LogRecord>
    {
        public List<Seen> Seen { get; } = [];

        public override void OnEnd(LogRecord data)
        {
            var attributes = (data.Attributes ?? []).ToList();
            var flattened = string.Join('|', attributes.Select(a => $"{a.Key}={a.Value}").Append(data.Body).Append(data.FormattedMessage).Append(data.Exception?.ToString()));
            Seen.Add(new Seen(data.Body, data.LogLevel, attributes, flattened));
        }
    }

    // harness.passes as the exporter reads it, by name and with its tags. Only HarnessLoop counts a
    // pass, and no other test class builds one.
    sealed class Passes : IDisposable
    {
        readonly MeterListener _listener = new();

        public HarnessMetrics Metrics { get; } = new();

        public List<(string? Outcome, int? Status, bool Tagged)> Seen { get; } = [];

        public Passes()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == HarnessMetrics.MeterName && instrument.Name == "harness.passes") listener.EnableMeasurementEvents(instrument);
            };
            _listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
            {
                string? outcome = null;
                int? status = null;
                var tagged = false;
                foreach (var tag in tags)
                {
                    if (tag.Key == "outcome") outcome = tag.Value as string;
                    if (tag.Key == "status")
                    {
                        tagged = true;
                        status = tag.Value as int?;
                    }
                }
                lock (Seen) Seen.Add((outcome, status, tagged));
            });
            _listener.Start();
        }

        public void Dispose()
        {
            _listener.Dispose();
            Metrics.Dispose();
        }
    }
}
