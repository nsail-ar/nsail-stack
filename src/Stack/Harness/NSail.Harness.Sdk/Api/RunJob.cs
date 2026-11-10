// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Planning;
using NSail.Messaging;
using System.ComponentModel.DataAnnotations;

namespace NSail.Harness.Api;

// A job sent by hand, past the plan's order and the machine's limits, never past the story's lock, Debug mode, a pause or the quota wall:
// a Release takes a product; a Review and a Gate a story and a PR; a Dev and an Analyze a story.
// Answers the started job's id.
[Http(Post, "api/harness/runs")]
public class RunJob : IMessage<string>
{
    public required WorkKind Kind { get; set; }
    [Range(1, 1_000_000)]
    public int? Story { get; set; }
    [Range(1, 1_000_000)]
    public int? Pr { get; set; }
    [MaxLength(50)]
    public string? Product { get; set; }
}
