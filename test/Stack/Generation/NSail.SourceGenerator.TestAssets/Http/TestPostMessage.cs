// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;
using System.ComponentModel.DataAnnotations;

namespace NSail.SourceGenerator.TestAssets.Http;

[Http(Post, "api/v1/samples/external/{id}")]
public class TestPostMessage : IMessage<ExternalPostResult>
{
    public string? Id { get; set; }

    [AsQuery]
    public bool Flag { get; set; }

    [Required]
    public string Name { get; set; } = "x";

    public DateTime? DateTime { get; set; }
}

public class ExternalPostResult
{
    public int Code { get; set; }
}
