// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;
using NSail.Messaging.Annotations;

namespace NSail.SourceGenerator.TestAssets.Http;

[Http(Head, "api/v1/probe")]
public class TestHeadMessage : IMessage
{
    public string? Tag { get; set; }
}

[Http(Options, "api/v1/probe/options")]
public class TestOptionsMessage : IMessage
{
    public string? Tag { get; set; }
}

[Http(Get, "api/v1/headed/{id}")]
public class TestHeaderMessage : IMessage<ExternalGetResult>
{
    public Guid Id { get; set; }

    [AsHeader]
    public string? Trace { get; set; }

    [AsHeader]
    public int? Attempt { get; set; }

    [AsHeader]
    public required string Tenant { get; set; }
}

[Http(Put, "api/v1/probe/{id}/put")]
public class TestPutMessage : IMessage
{
    public Guid Id { get; set; }

    public string? Tag { get; set; }
}

[Http(Patch, "api/v1/probe/{id}/patch")]
public class TestPatchMessage : IMessage
{
    public Guid Id { get; set; }

    public string? Tag { get; set; }
}

[Http(Delete, "api/v1/probe/{id}/delete")]
public class TestDeleteMessage : IMessage
{
    public Guid Id { get; set; }

    public string? Tag { get; set; }
}
