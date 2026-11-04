// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness;

// A seat: how it signs what the harness posts for it, the model it runs on unless a story's label
// names another, and its prompt — inline, or a file in the default repository's main.
public sealed class AgentOptions
{
    public string Signature { get; set; } = "";
    public string Model { get; set; } = "";
    public string? Prompt { get; set; }
    public string? PromptFile { get; set; }

    // The reviewer's addendum from its third read of a PR on.
    public string? NarrowPrompt { get; set; }
    public string? NarrowPromptFile { get; set; }

    // The label the analyst's own ruling leaves on a story, if any.
    public string? RuledLabel { get; set; }

    // A worker's narrower words, read after the seat's own prompt (WorkerOptions.Prompt/PromptFile).
    public string? WorkerPrompt { get; set; }
    public string? WorkerPromptFile { get; set; }

    public AgentOptions Clone()
    {
        return (AgentOptions)MemberwiseClone();
    }
}
