// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Jobs;

// A seat's prompt: inline, or the project's own file read from main's tree — never a story branch's,
// which may predate a change to it. Its `__NAME__` slots are filled here.
public static class SeatPrompt
{
    public static async Task<string> Read(string tree, string? inline, string? file, IReadOnlyDictionary<string, string> slots, CancellationToken ct)
    {
        // A prompt file beside the harness's own configuration is the harness's, for every repository it
        // works; any other is the repository's, at the job's commit.
        var own = file is null ? null : Path.Combine(AppContext.BaseDirectory, file);
        var text = !string.IsNullOrWhiteSpace(inline) ? inline : await File.ReadAllTextAsync(own is not null && File.Exists(own) ? own : Path.Combine(tree, file!), ct);
        foreach (var (name, value) in slots) text = text.Replace($"__{name}__", value);
        return text;
    }

    // The seat's prompt, then its worker's narrower words when the worker has any: the layers add up.
    public static async Task<string> Read(string tree, AgentOptions seat, IReadOnlyDictionary<string, string> slots, CancellationToken ct)
    {
        var text = await Read(tree, seat.Prompt, seat.PromptFile, slots, ct);
        if (string.IsNullOrWhiteSpace(seat.WorkerPrompt) && string.IsNullOrWhiteSpace(seat.WorkerPromptFile)) return text;
        return text.TrimEnd() + "\n\n" + await Read(tree, seat.WorkerPrompt, seat.WorkerPromptFile, slots, ct);
    }
}
