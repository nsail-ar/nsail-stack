// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text.Json;
using NSail.Harness.Locks;

namespace NSail.Harness.GitHub;

// One story, one job at a time, across every harness: `refs/locks/{key}-{n}` is created through the
// API, and GitHub creates a ref atomically — the second creator gets 422 and stands down. The ref
// points at an annotated tag whose message is the holder.
public sealed class RefLock(GitHubClient gh, string repo, string key, string defaultBranch) : IStoryLock
{
    string Name(int story)
    {
        return $"locks/{key}-{story}";
    }

    public async Task<bool> TryAcquire(int story, LockHolder holder, CancellationToken ct = default)
    {
        var main = await gh.Get($"repos/{repo}/git/ref/heads/{Uri.EscapeDataString(defaultBranch)}", ct);
        var tag = await gh.Send(HttpMethod.Post, $"repos/{repo}/git/tags", new
        {
            tag = $"lock-{key}-{story}",
            message = JsonSerializer.Serialize(holder),
            @object = main.GetProperty("object").GetProperty("sha").GetString(),
            type = "commit",
        }, ct);
        try
        {
            await gh.Send(HttpMethod.Post, $"repos/{repo}/git/refs", new { @ref = $"refs/{Name(story)}", sha = tag.GetProperty("sha").GetString() }, ct);
            return true;
        }
        catch (GitHubException e) when (e.HttpStatus == HttpStatusCode.UnprocessableEntity)
        {
            return false;
        }
    }

    public async Task<LockHolder?> Holder(int story, CancellationToken ct = default)
    {
        try
        {
            var r = await gh.Get($"repos/{repo}/git/ref/{Name(story)}", ct);
            return await HolderAt(r.GetProperty("object"), ct);
        }
        catch (GitHubException e) when (e.HttpStatus == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task Release(int story, LockHolder owner, CancellationToken ct = default)
    {
        try
        {
            if (await Holder(story, ct) is not { } current || !current.Is(owner)) return;
            await gh.Send(HttpMethod.Delete, $"repos/{repo}/git/refs/{Name(story)}", ct: ct);
        }
        catch (GitHubException e) when (e.HttpStatus is HttpStatusCode.NotFound or HttpStatusCode.UnprocessableEntity)
        {
        }
    }

    // A lock's tag never changes once written, so its holder is read once per tag.
    static readonly ConcurrentDictionary<string, LockHolder> HolderByTag = new();

    public async Task<IReadOnlyDictionary<int, string>> Holders(CancellationToken ct = default)
    {
        var holders = new Dictionary<int, string>();
        List<JsonElement> refs;
        try
        {
            refs = await gh.GetAll($"repos/{repo}/git/matching-refs/locks/{key}-", ct: ct);
        }
        catch (GitHubException e) when (e.HttpStatus == HttpStatusCode.NotFound)
        {
            return holders;
        }
        foreach (var r in refs)
        {
            var name = r.GetProperty("ref").GetString() ?? "";
            var prefix = $"refs/locks/{key}-";
            if (!name.StartsWith(prefix, StringComparison.Ordinal) || !int.TryParse(name[prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var n)) continue;
            holders[n] = (await HolderAt(r.GetProperty("object"), ct)).Machine;
        }
        return holders;
    }

    async Task<LockHolder> HolderAt(JsonElement obj, CancellationToken ct)
    {
        var sha = obj.GetProperty("sha").GetString() ?? "";
        if (obj.GetProperty("type").GetString() != "tag") return Unknown;
        if (HolderByTag.TryGetValue(sha, out var known)) return known;
        var tag = await gh.Get($"repos/{repo}/git/tags/{sha}", ct);
        return HolderByTag[sha] = Parse(tag.GetProperty("message").GetString());
    }

    static readonly LockHolder Unknown = new("?", "?", "?", DateTimeOffset.MinValue);

    internal static LockHolder Parse(string? message)
    {
        try
        {
            return JsonSerializer.Deserialize<LockHolder>(message ?? "") ?? Unknown;
        }
        catch (JsonException)
        {
            return Unknown;
        }
    }
}
