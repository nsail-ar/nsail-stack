// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using NSail.Harness.Watching;

namespace NSail.Harness.OpenObserve;

// The error lines of OpenObserve's logs stream, over its search API (SQL). The store's columns are
// the OpenTelemetry export's as OpenObserve flattens them: `service_name`, the logger in
// `instrumentation_library_name`, the message template in `body`, `exception_type` and
// `exception_stacktrace`, the cell in `nsail_tenant`, `trace_id` when the line had one; a failed
// pass's `providercall`, `providerstatus` and `ratelimitremaining` (its placeholders, in any case).
public sealed class OpenObserveLogSource(HttpClient http, OpenObserveOptions options) : ILogSource
{
    public string Name => "OpenObserve";

    public async Task<IReadOnlyList<LoggedError>> Errors(DateTimeOffset since, DateTimeOffset until, int max, CancellationToken ct = default)
    {
        var endpoint = options.Endpoint!.TrimEnd('/');
        var sql = $"SELECT * FROM \"{options.Stream}\" WHERE severity IN ('Error', 'Critical') ORDER BY _timestamp ASC";
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{endpoint}/api/{options.Organization}/_search")
        {
            Content = JsonContent.Create(new { query = new { sql, start_time = Microseconds(since), end_time = Microseconds(until), from = 0, size = max } }),
        };
        if (options.ResolveUsername() is { } user && options.ResolvePassword() is { } password)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}")));
        }
        using var response = await http.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new ProviderException($"OpenObserve search answered {(int)response.StatusCode}: {text[..Math.Min(300, text.Length)]}", (int)response.StatusCode);
        }
        using var json = JsonDocument.Parse(text);
        return Rows(json.RootElement, endpoint, options);
    }

    internal static IReadOnlyList<LoggedError> Rows(JsonElement answer, string endpoint, OpenObserveOptions options)
    {
        if (!answer.TryGetProperty("hits", out var hits) || hits.ValueKind != JsonValueKind.Array) return [];
        var rows = new List<LoggedError>();
        foreach (var hit in hits.EnumerateArray())
        {
            if (!hit.TryGetProperty("_timestamp", out var stamp) || stamp.ValueKind != JsonValueKind.Number) continue;
            var at = DateTimeOffset.UnixEpoch.AddTicks(stamp.GetInt64() * 10);
            rows.Add(new LoggedError(
                at,
                Text(hit, "service_name") ?? "",
                Text(hit, "instrumentation_library_name") ?? "",
                Text(hit, "body") ?? "",
                Text(hit, "exception_type"),
                Text(hit, "exception_stacktrace"),
                Text(hit, "nsail_tenant") ?? "",
                Text(hit, "trace_id"),
                Link(endpoint, options, at),
                Field(hit, "providercall") is { ValueKind: JsonValueKind.String } call && call.GetString() is { Length: > 0 } named ? named : null,
                Number(hit, "providerstatus") is { } status and >= int.MinValue and <= int.MaxValue ? (int)status : null,
                Number(hit, "ratelimitremaining")));
        }
        return rows;
    }

    // The logs page around the line: a minute each side.
    static Uri Link(string endpoint, OpenObserveOptions options, DateTimeOffset at)
    {
        var from = Microseconds(at.AddMinutes(-1)).ToString(CultureInfo.InvariantCulture);
        var to = Microseconds(at.AddMinutes(1)).ToString(CultureInfo.InvariantCulture);
        return new Uri($"{endpoint}/web/logs?org_identifier={Uri.EscapeDataString(options.Organization)}&stream_type=logs&stream={Uri.EscapeDataString(options.Stream)}&from={from}&to={to}");
    }

    static string? Text(JsonElement hit, string name)
    {
        return hit.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } s ? s : null;
    }

    // A record attribute's column, in whatever case the store kept the placeholder's name.
    static JsonElement? Field(JsonElement hit, string name)
    {
        foreach (var column in hit.EnumerateObject())
        {
            if (string.Equals(column.Name, name, StringComparison.OrdinalIgnoreCase)) return column.Value;
        }
        return null;
    }

    // An attribute OpenObserve stored as a number, or as the number's text.
    static long? Number(JsonElement hit, string name)
    {
        if (Field(hit, name) is not { } v) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n)) return n;
        return v.ValueKind == JsonValueKind.String && long.TryParse(v.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    static long Microseconds(DateTimeOffset at)
    {
        return (at - DateTimeOffset.UnixEpoch).Ticks / 10;
    }
}
