// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.Json;
using NSail.Harness.Agents;

namespace NSail.Harness.Claude;

public sealed record StreamSignals(bool QuotaRefused, DateTimeOffset? QuotaUntil, string? Result, IReadOnlyList<UsageWindow> Usage)
{
    // The usage wall reads only from the stream's own top-level objects — a rate-limit event
    // rejected, the API's usage_limit_reached, a result with status 429 — never from prose,
    // since a story thread can quote "hit your weekly limit" (two false pauses did).
    public static StreamSignals Read(string output)
    {
        var refused = false;
        long resets = 0;
        string? result = null;
        IReadOnlyList<UsageWindow> usage = [];
        foreach (var line in output.Split('\n'))
        {
            var t = line.Trim();
            if (t.Length == 0 || t[0] != '{') continue;
            JsonElement o;
            try
            {
                using var doc = JsonDocument.Parse(t);
                o = doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                continue;
            }
            if (o.ValueKind != JsonValueKind.Object) continue;
            var type = Str(o, "type");
            if (type == "rate_limit_event" && o.TryGetProperty("rate_limit_info", out var windows) && Windows(windows) is { Count: > 0 } read) usage = read;
            if (type == "rate_limit_event" && o.TryGetProperty("rate_limit_info", out var info) && Str(info, "status") == "rejected")
            {
                refused = true;
                resets = Math.Max(resets, Long(info, "resetsAt"));
            }
            if (Str(o, "api_error") == "usage_limit_reached")
            {
                refused = true;
                if (o.TryGetProperty("api_error_params", out var ps) && ps.TryGetProperty("rate_limit_info", out var ri))
                {
                    resets = Math.Max(resets, Long(ri, "resetsAt"));
                }
            }
            if (type == "result")
            {
                if (o.TryGetProperty("api_error_status", out var s) && s.ValueKind == JsonValueKind.Number && s.GetInt32() == 429) refused = true;
                result = Str(o, "result") ?? result;
            }
        }
        DateTimeOffset? until = resets > 0 ? DateTimeOffset.FromUnixTimeSeconds(resets) : null;
        return new StreamSignals(refused, until, result, usage);
    }

    // A rate-limit event's quota windows (`unifiedWindows`: five_hour, seven_day, ...), each with
    // its share used and its reset; a window whose length the name does not tell is left out.
    internal static IReadOnlyList<UsageWindow> Windows(JsonElement info)
    {
        if (!info.TryGetProperty("unifiedWindows", out var all) || all.ValueKind != JsonValueKind.Object) return [];
        var windows = new List<UsageWindow>();
        foreach (var w in all.EnumerateObject())
        {
            TimeSpan? length = w.Name.StartsWith("five_hour", StringComparison.Ordinal) ? TimeSpan.FromHours(5)
                : w.Name.StartsWith("seven_day", StringComparison.Ordinal) ? TimeSpan.FromDays(7)
                : null;
            if (length is null || !w.Value.TryGetProperty("utilization", out var u) || u.ValueKind != JsonValueKind.Number) continue;
            windows.Add(new UsageWindow(w.Name, u.GetDouble(), DateTimeOffset.FromUnixTimeSeconds(Long(w.Value, "resetsAt")), length.Value));
        }
        return windows;
    }

    static string? Str(JsonElement o, string name)
    {
        return o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    static long Long(JsonElement o, string name)
    {
        return o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0;
    }
}
