// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text;
using System.Text.Json;
using NSail.Harness.Jobs;
using NSail.Harness.Planning;

namespace NSail.Harness.Lanes;

// What a running job is doing now and when its log last moved, read off the log at each look. An
// agent's line is the last text it said itself, never a command or a tool's input or output (where
// env values and tokens show); a build's is its step. A log that cannot be read leaves both unset.
public static class JobActivity
{
    public const int LineCap = 200;
    public const string Waiting = "waiting for its build turn";
    public const string Building = "building";
    public const string Testing = "testing";

    // Only the log's end is read: a dev log runs to megabytes and the status page refreshes often.
    const int Window = 1 << 20;

    // The job's kind of work: an agent's (a seat with no command), a build's (a gate), or another.
    public enum Shape
    {
        Agent,
        Build,
        Other,
    }

    public static Shape ShapeOf(WorkItem item, JobStep? step)
    {
        if (item.Kind is WorkKind.Gate or WorkKind.Main) return Shape.Build;
        if (item.Kind is WorkKind.Dev or WorkKind.Analyze or WorkKind.Review or WorkKind.Qa && step is { Command: null }) return Shape.Agent;
        return Shape.Other;
    }

    // The limit is the one the job already runs under: an agent's longest foreground command (its
    // step's minutes where the agent sets none), a build's step timeout once it holds its turn, any
    // other job's step timeout. A build waiting for its turn is a queue, never stale.
    public static RunningJob Read(RunningJob job, JobStep? step, TimeSpan? longestCommand, bool waitingTurn)
    {
        var shape = ShapeOf(job.Item, step);
        var (lines, wrote) = Tail(job.LogPath);
        string? doing = shape switch
        {
            Shape.Agent => lines is null ? null : LastWords(lines),
            Shape.Build => waitingTurn ? Waiting : lines is null ? null : BuildStep(lines),
            _ => null,
        };
        return job with { Doing = doing, LastWrote = wrote, SilenceLimit = Limit(job.Item, shape, step, longestCommand, waitingTurn) };
    }

    static TimeSpan? Limit(WorkItem item, Shape shape, JobStep? step, TimeSpan? longestCommand, bool waitingTurn)
    {
        if (step is null) return null;
        return shape switch
        {
            Shape.Agent => longestCommand ?? TimeSpan.FromMinutes(step.Minutes),
            Shape.Build when waitingTurn => null,
            Shape.Build => item.Kind == WorkKind.Main ? MainGateJob.Timeout(step) : GateJob.Timeout(step),
            _ => TimeSpan.FromMinutes(step.Minutes),
        };
    }

    // The log's last lines, oldest first, and when it was last written; nothing when it cannot be read.
    internal static (IReadOnlyList<string>? Lines, DateTimeOffset? Wrote) Tail(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            // The open handle's time: a directory entry can lag behind a file still held for writing.
            var wrote = new DateTimeOffset(File.GetLastWriteTimeUtc(stream.SafeFileHandle), TimeSpan.Zero);
            var skip = Math.Max(0, stream.Length - Window);
            stream.Seek(skip, SeekOrigin.Begin);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var lines = reader.ReadToEnd().Split('\n').Select(l => l.TrimEnd('\r')).ToList();
            // A window that starts mid-line drops that line's broken head.
            if (skip > 0 && lines.Count > 0) lines.RemoveAt(0);
            return (lines, wrote);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return (null, null);
        }
    }

    // The last text block of an assistant event in a stream-json log, its first line cut to LineCap.
    internal static string? LastWords(IReadOnlyList<string> lines)
    {
        for (var i = lines.Count - 1; i >= 0; i--)
        {
            if (!lines[i].StartsWith('{') || !lines[i].Contains("\"assistant\"", StringComparison.Ordinal)) continue;
            if (TextOf(lines[i]) is { } text && Line(text) is { } line) return line;
        }
        return null;
    }

    static string? TextOf(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var e = doc.RootElement;
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty("type", out var type) || type.GetString() != "assistant") return null;
            if (!e.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object) return null;
            if (!message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array) return null;
            string? last = null;
            foreach (var c in content.EnumerateArray())
            {
                if (c.ValueKind != JsonValueKind.Object || !c.TryGetProperty("type", out var ct) || ct.GetString() != "text") continue;
                if (c.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String && text.GetString() is { } s && s.Trim().Length > 0) last = s;
            }
            return last;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // A text's first non-empty line, at most LineCap characters, with «…» where it was cut.
    internal static string? Line(string text)
    {
        var first = text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
        if (first is null) return null;
        if (first.Length <= LineCap && !text.Trim().Contains('\n')) return first;
        return first[..Math.Min(first.Length, LineCap - 1)].TrimEnd() + "…";
    }

    // A build's step, off the last step its log names: the house builder writes `> dotnet test …`
    // before its tests, `dotnet test` prints `Test run for …` as it starts each project.
    internal static string BuildStep(IReadOnlyList<string> lines)
    {
        for (var i = lines.Count - 1; i >= 0; i--)
        {
            var line = lines[i];
            if (line.StartsWith("> dotnet test", StringComparison.Ordinal) || line.Contains("Test run for ", StringComparison.Ordinal)) return Testing;
            if (line.StartsWith("> dotnet ", StringComparison.Ordinal)) return Building;
        }
        return Building;
    }
}
