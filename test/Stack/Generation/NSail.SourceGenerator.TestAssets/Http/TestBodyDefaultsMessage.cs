// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;

namespace NSail.SourceGenerator.TestAssets.Http;

[Http(Post, "api/v1/samples/defaults")]
public class TestBodyDefaultsMessage : IMessage
{
    public required string Title { get; set; }

    public required int Sequence { get; set; }

    public int Retries { get; set; } = 3;

    public bool Active { get; set; } = true;

    public DateTime? ExpiresOn { get; set; }

    public List<string> Tags { get; set; } = [];

    public string? Notes { get; set; } = "n/a";
}
