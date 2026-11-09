// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.Json;

namespace NSail.Harness.Lanes;

// How the harness's small state files are read and written: a read never stops the host (a file
// zero-length, torn or unreadable is moved aside, said in the log, and the state starts empty),
// and a write lands whole or not at all (a temp file moved over the original).
internal static class StateFile
{
    public static T Read<T>(string path, Func<T> empty, JsonSerializerOptions? json = null, ILogger? log = null)
    {
        if (!File.Exists(path)) return empty();
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), json) ?? throw new JsonException("the file holds null");
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            var aside = $"{path}.bad-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
            try
            {
                File.Move(path, aside, overwrite: true);
            }
            catch (Exception m) when (m is IOException or UnauthorizedAccessException)
            {
                aside = "(could not be moved)";
            }
            log?.LogWarning(e, "state file {Path} is unreadable — moved to {Aside}, starting empty", path, aside);
            return empty();
        }
    }

    public static void Write<T>(string path, T value, JsonSerializerOptions? json = null)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, json));
        File.Move(tmp, path, overwrite: true);
    }
}
