// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net;
using System.Text;
using System.Text.Json;

namespace NSail.Telemetry.Tests;

/// <summary>Answers whatever it was handed and keeps the request, so a test can read the
/// query that went out — the store itself is not reachable from a build.</summary>
public sealed class Store : HttpMessageHandler
{
    readonly HttpStatusCode _status;
    readonly string _body;

    public Store(HttpStatusCode status, string body)
    {
        _status = status;
        _body = body;
    }

    public string? Sent { get; private set; }

    public string? Credential { get; private set; }

    public Uri? Address { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Address = request.RequestUri;
        Credential = request.Headers.TryGetValues("Authorization", out var values) ? string.Join(string.Empty, values) : null;
        Sent = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

        return new HttpResponseMessage(_status)
        {
            Content = new StringContent(_body, Encoding.UTF8, "application/json"),
        };
    }
}

// The one implementation of the seam: what it asks the store for, what it makes of the
// answer, and what it does when the store will not answer at all.
public sealed class FailureLogTests
{
    const string Endpoint = "https://store.invalid";

    const string Credential = "Basic cmVhZDpvbmx5";

    [Fact]
    public async Task The_query_is_the_callers_own_failures_newest_first()
    {
        var user = Guid.NewGuid();
        using var store = new Store(HttpStatusCode.OK, "{\"hits\":[]}");

        await Read(store, new FailureLogQuery(user, DateTimeOffset.UtcNow.AddHours(-24), 20));

        var query = Query(store);
        var sql = query.GetProperty("sql").GetString() ?? string.Empty;

        Assert.Contains($"nsail_user = '{user}'", sql, StringComparison.Ordinal);
        Assert.Contains("nsail_rule", sql, StringComparison.Ordinal);
        Assert.Contains("nsail_outcome IN ('refused', 'failed')", sql, StringComparison.Ordinal);
        Assert.Contains("ORDER BY start_time DESC", sql, StringComparison.Ordinal);
        Assert.Contains("FROM \"default\"", sql, StringComparison.Ordinal);

        Assert.Equal(20, query.GetProperty("size").GetInt32());
        Assert.Equal(new Uri($"{Endpoint}/api/default/_search"), store.Address);
        Assert.Equal(Credential, store.Credential);
    }

    // The bound travels as the store's own unit, microseconds, and it is the one the caller
    // asked for: a reader that widened it would answer with entries the door does not admit.
    [Fact]
    public async Task The_bound_is_the_period_the_caller_asked_for()
    {
        var since = DateTimeOffset.UtcNow.AddHours(-24);
        using var store = new Store(HttpStatusCode.OK, "{\"hits\":[]}");

        await Read(store, new FailureLogQuery(Guid.NewGuid(), since, 20));

        var query = Query(store);

        Assert.Equal(since.ToUnixTimeMilliseconds() * 1000, query.GetProperty("start_time").GetInt64());
        Assert.True(query.GetProperty("end_time").GetInt64() >= query.GetProperty("start_time").GetInt64());
    }

    [Fact]
    public async Task A_refusal_carries_its_code_and_a_crash_its_type()
    {
        var at = DateTimeOffset.UtcNow.AddMinutes(-5);

        var body = JsonSerializer.Serialize(new
        {
            hits = new object[]
            {
                new
                {
                    start_time = at.ToUnixTimeMilliseconds() * 1000,
                    span_name = "Sales.Boxes.CloseBox",
                    trace_id = "9f8e7d",
                    nsail_problem = "RuleViolation",
                    nsail_rule = "RuleViolation.BoxAlreadyClosed",
                },
                new
                {
                    start_time = at.AddMinutes(-1).ToUnixTimeMilliseconds() * 1000,
                    span_name = "Sales.Boxes.ListBoxes",
                    trace_id = "1a2b3c",
                    nsail_failure = "System.InvalidOperationException",
                },
            },
        });

        using var store = new Store(HttpStatusCode.OK, body);

        var failures = await Read(store, new FailureLogQuery(Guid.NewGuid(), at.AddHours(-24), 20));

        Assert.Equal(2, failures.Count);

        var refusal = failures[0];

        Assert.Equal("Sales.Boxes.CloseBox", refusal.Operation);
        Assert.Equal("RuleViolation", refusal.ProblemCode);
        Assert.Equal("RuleViolation.BoxAlreadyClosed", refusal.Rule);
        Assert.Null(refusal.FailureType);
        Assert.Equal("9f8e7d", refusal.TraceId);
        Assert.Equal(at.ToUnixTimeMilliseconds(), refusal.At.ToUnixTimeMilliseconds());

        var crash = failures[1];

        Assert.Equal("System.InvalidOperationException", crash.FailureType);
        Assert.Null(crash.ProblemCode);
        Assert.Null(crash.Rule);
    }

    [Fact]
    public async Task A_store_that_refuses_is_one_failure_and_not_its_own_words()
    {
        using var store = new Store(HttpStatusCode.Forbidden, "{\"message\":\"token expired for read@nsail\"}");

        var thrown = await Assert.ThrowsAsync<FailureLogException>(() =>
            Read(store, new FailureLogQuery(Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(-24), 20)));

        Assert.DoesNotContain("read@nsail", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_store_that_answers_something_else_is_one_failure_too()
    {
        using var store = new Store(HttpStatusCode.OK, "<html>bad gateway</html>");

        await Assert.ThrowsAsync<FailureLogException>(() =>
            Read(store, new FailureLogQuery(Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(-24), 20)));
    }

    static JsonElement Query(Store store)
    {
        Assert.NotNull(store.Sent);

        return JsonDocument.Parse(store.Sent).RootElement.GetProperty("query");
    }

    static async Task<IReadOnlyList<LoggedFailure>> Read(Store store, FailureLogQuery query)
    {
        using var client = new HttpClient(store, disposeHandler: false);

        var log = new OpenObserveFailureLog(client, new TelemetryOptions
        {
            SearchEndpoint = Endpoint,
            SearchAuthorization = Credential,
        });

        return await log.Read(query, CancellationToken.None);
    }
}
