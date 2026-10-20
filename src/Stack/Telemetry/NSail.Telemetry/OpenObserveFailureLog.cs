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
/// not answering, answering something else — is one <see cref="FailureLogException"/>,
/// because there is nothing a caller could do differently about any of them.</summary>
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

        Answer? answer;

        try
        {
            answer = await response.Content
                .ReadFromJsonAsync<Answer>(JsonOptions.Wire, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException exception)
        {
            throw new FailureLogException("The failure log answered something that is not a search result.", exception);
        }

        return (answer?.Hits ?? []).Select(Failure).ToList();
    }

    /// <summary>The store's column for a span tag: its own name with the dots flattened,
    /// derived from the tag rather than written again, so renaming a tag cannot leave this
    /// query reading a column nobody writes.</summary>
    static string Column(string tag)
    {
        return tag.Replace('.', '_');
    }

    // A traces stream is counted in microseconds, both in the bounds a search is given and in
    // the column it answers with.
    static long Microseconds(DateTimeOffset moment)
    {
        return moment.ToUnixTimeMilliseconds() * 1000;
    }

    static DateTimeOffset At(long microseconds)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(microseconds / 1000);
    }

    static LoggedFailure Failure(Dictionary<string, JsonElement> hit)
    {
        return new LoggedFailure(
            At(Number(hit, TimeColumn)),
            Text(hit, NameColumn) ?? string.Empty,
            Text(hit, Column(MessageSpan.ProblemTag)),
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

    static long Number(Dictionary<string, JsonElement> hit, string column)
    {
        if (!hit.TryGetValue(column, out var value) || !value.TryGetInt64(out var number))
        {
            return 0;
        }

        return number;
    }

    /// <summary>The caller's own failures, newest first. Every value interpolated is either a
    /// constant of this Stack or a Guid — no string anybody typed reaches this SQL, which is
    /// what makes composing it rather than binding it safe.</summary>
    string Sql(FailureLogQuery query)
    {
        return $"SELECT {TimeColumn}, {NameColumn}, {TraceColumn}, "
            + $"{Column(MessageSpan.ProblemTag)}, {Column(MessageSpan.FailureTag)} "
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

    sealed record Answer([property: JsonPropertyName("hits")] IReadOnlyList<Dictionary<string, JsonElement>>? Hits);
}
