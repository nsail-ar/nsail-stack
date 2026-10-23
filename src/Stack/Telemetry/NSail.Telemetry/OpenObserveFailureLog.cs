// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using NSail.Serialization;

namespace NSail.Telemetry;

/// <summary>The one implementation of <see cref="IFailureLog"/>, over OpenObserve's search
/// API. The vendor stops here: what it answers is <see cref="LoggedFailure"/>, so a kit or a
/// screen that reads a person's own failures names no store and would not have to change if
/// the install moved to another one. Every way the store can let the caller down — refusing,
/// not answering, answering something else, answering only rows this reader cannot read — is
/// one <see cref="FailureLogException"/>, because there is nothing a caller could do
/// differently about any of them. A single unreadable row among readable ones is dropped
/// instead: one row the store wrote oddly is not an outage.</summary>
public sealed class OpenObserveFailureLog : IFailureLog
{
    // The store's own columns for a traces stream, which are its names and not OpenTelemetry's.
    const string TraceColumn = "trace_id";

    const string NameColumn = "span_name";

    const string TimeColumn = "start_time";

    public OpenObserveFailureLog(HttpClient client, TelemetryOptions options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);

        Client = client;
        Options = options;
    }

    HttpClient Client { get; }

    TelemetryOptions Options { get; }

    public async Task<IReadOnlyList<LoggedFailure>> Read(FailureLogQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var search = new Search(new Bounds(
            Sql(query),
            Microseconds(query.Since),
            Microseconds(DateTimeOffset.UtcNow),
            query.Max));

        using var request = new HttpRequestMessage(HttpMethod.Post, Options.SearchUri())
        {
            Content = JsonContent.Create(search, options: JsonOptions.Wire),
        };

        // The credential arrives whole, scheme included, so nothing is composed here: the day
        // the store stops authenticating with Basic, the seam is a deploy variable and not an
        // edit.
        request.Headers.TryAddWithoutValidation("Authorization", Options.SearchAuthorization);

        using var response = await Send(request, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new FailureLogException($"The failure log answered {(int)response.StatusCode}.");
        }

        // Read as text and deserialized here rather than through ReadFromJsonAsync, which
        // validates the media type first and throws NotSupportedException for anything that is
        // not application/json: a store behind an authentication portal answers 200 text/html,
        // and that has to be the one exception this seam raises like every other way the store
        // lets a caller down. JsonSerializer can only fail with JsonException.
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        Answer? answer;

        try
        {
            answer = JsonSerializer.Deserialize<Answer>(body, JsonOptions.Wire);
        }
        catch (JsonException exception)
        {
            throw new FailureLogException("The failure log answered something that is not a search result.", exception);
        }

        // A body that deserialized and still carries no hits is the same answer as one that did
        // not deserialize at all: a search result has that member, and {}, a portal's or a
        // gateway's {"code":200,"message":"..."} and an explicit "hits":null are all something
        // else. The empty list the absent member reads as is the one answer this door must never
        // invent — it says "you have no recent failures", for as long as nobody looks.
        if (answer?.Hits is not { } hits)
        {
            throw new FailureLogException("The failure log answered something that is not a search result.");
        }

        var failures = hits.Select(Failure).OfType<LoggedFailure>().ToList();

        // One unreadable row among readable ones is dropped: a single row the store wrote
        // oddly is not an outage. A response that brought rows and yielded NONE is — the
        // column, or its unit, is not what this reader takes it for — and the empty list it
        // would otherwise answer with reads as "you have no recent failures", for as long as
        // nobody looks.
        if (hits.Count > 0 && failures.Count == 0)
        {
            throw new FailureLogException("The failure log answered rows this reader could not read.");
        }

        return failures;
    }

    /// <summary>The store's column for a span tag: its own name with the dots flattened,
    /// derived from the tag rather than written again, so renaming a tag cannot leave this
    /// query reading a column nobody writes.</summary>
    static string Column(string tag)
    {
        return tag.Replace('.', '_');
    }

    // A traces stream is counted in microseconds, which is what the bounds a search is given
    // are written in.
    static long Microseconds(DateTimeOffset moment)
    {
        return moment.ToUnixTimeMilliseconds() * 1000;
    }

    // What the stream answers WITH is not assumed, though, because the unit is the one thing
    // about this store no build can check and getting it wrong is worse than crashing: a list
    // whose order and whose whole bound are by WHEN would carry invented dates — every failure
    // around 1970 if the column turned out to be milliseconds, and an exception out of
    // FromUnixTime* if it turned out to be the nanoseconds OTLP itself counts in. So it is
    // derived per row: the four candidate units sit three orders of magnitude apart, so any
    // instant this door can be asked about falls inside the window in exactly one of them. A
    // value in none is a time this reader cannot read, and its row is dropped like a row that
    // carried no time at all.
    // The four candidate units, largest first, and the window a stamp divided by one of them
    // has to land in: 2001-09-09 to 2096-10-02, in seconds. Wide enough that no install's
    // clock argues with it, narrow enough that the four bands stay disjoint.
    static readonly long[] Units = [1_000_000_000, 1_000_000, 1_000, 1];

    const long Earliest = 1_000_000_000;

    const long Latest = 4_000_000_000;

    static DateTimeOffset? At(long stamp)
    {
        foreach (var unit in Units)
        {
            var seconds = stamp / unit;

            if (seconds < Earliest || seconds > Latest)
            {
                continue;
            }

            // Never a multiplication that could overflow: a value whose seconds are in the
            // window is at most Latest, so the one unit that multiplies stays well inside.
            return DateTimeOffset.FromUnixTimeMilliseconds(unit >= 1_000 ? stamp / (unit / 1_000) : stamp * 1_000);
        }

        return null;
    }

    // A hit with no readable timestamp is dropped rather than dated: a made-up date would file
    // the failure as the oldest entry in a list whose whole order is by when. A null element in
    // the array is read the same way — there is no object to take a column from — and not
    // dereferenced: an array's elements are the store's word, like every value inside one.
    static LoggedFailure? Failure(Dictionary<string, JsonElement>? hit)
    {
        if (hit is null || Number(hit, TimeColumn) is not { } stamp || At(stamp) is not { } at)
        {
            return null;
        }

        return new LoggedFailure(
            at,
            Text(hit, NameColumn) ?? string.Empty,
            Text(hit, Column(MessageSpan.ProblemTag)),
            Text(hit, Column(MessageSpan.RuleTag)),
            Text(hit, Column(MessageSpan.FailureTag)),
            Text(hit, TraceColumn));
    }

    static string? Text(Dictionary<string, JsonElement> hit, string column)
    {
        if (!hit.TryGetValue(column, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }

    // The ValueKind is checked for the same reason Text checks it, and with more at stake:
    // TryGetInt64 does not answer false for an element that is not a number, it throws.
    static long? Number(Dictionary<string, JsonElement> hit, string column)
    {
        if (!hit.TryGetValue(column, out var value) || value.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return value.TryGetInt64(out var number) ? number : null;
    }

    /// <summary>The caller's own failures, newest first. What is interpolated is a constant of
    /// this Stack, a Guid, or the install's own configured stream name — nothing a user of the
    /// application can reach, which is what makes composing this rather than binding it
    /// safe.</summary>
    string Sql(FailureLogQuery query)
    {
        return $"SELECT {TimeColumn}, {NameColumn}, {TraceColumn}, "
            + $"{Column(MessageSpan.ProblemTag)}, {Column(MessageSpan.RuleTag)}, {Column(MessageSpan.FailureTag)} "
            + $"FROM \"{Options.SearchStream}\" "
            + $"WHERE {Column(MessageSpan.UserTag)} = '{query.UserId}' "
            + $"AND {Column(MessageSpan.OutcomeTag)} IN ('{MessageSpan.Refused}', '{MessageSpan.Failed}') "
            + $"ORDER BY {TimeColumn} DESC";
    }

    async Task<HttpResponseMessage> Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await Client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new FailureLogException("The failure log did not answer.", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new FailureLogException("The failure log took too long to answer.", exception);
        }
    }

    sealed record Search([property: JsonPropertyName("query")] Bounds Query);

    sealed record Bounds(
        [property: JsonPropertyName("sql")] string Sql,
        [property: JsonPropertyName("start_time")] long StartTime,
        [property: JsonPropertyName("end_time")] long EndTime,
        [property: JsonPropertyName("size")] int Size)
    {
        [JsonPropertyName("from")]
        public int From
        {
            get { return 0; }
        }
    }

    sealed record Answer([property: JsonPropertyName("hits")] IReadOnlyList<Dictionary<string, JsonElement>?>? Hits);
}
