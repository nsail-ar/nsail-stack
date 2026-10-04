// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;

namespace NSail.SourceGenerator.TestAssets.Http;

[Http(Post, "api/v1/void-samples")]
public class TestVoidMessage : IMessage
{
    public string? Name { get; set; }
}
