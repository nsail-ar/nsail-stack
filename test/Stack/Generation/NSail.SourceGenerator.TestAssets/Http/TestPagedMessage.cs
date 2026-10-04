// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;

namespace NSail.SourceGenerator.TestAssets.Http;

[Http(Get, "api/v1/samples/paged")]
public class TestPagedMessage : IMessage<List<ExternalGetResult>>
{
    public required Guid OwnerId { get; set; }

    public int PageIndex { get; set; }

    public int PageSize { get; set; } = 10;

    public string? Search { get; set; }
}
